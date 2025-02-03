using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class CancellationAndTimeoutTests
{

    public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
    {
    }

    public class DConfiguration : DeviceConfiguration
    {
        public DConfiguration()
        {
        }

        public DConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte();
            RandomSeed = r.ReadInt32();
        }

        public int RandomSeed { get; set; }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.Write( RandomSeed );
        }
    }

    public class D : Device<DConfiguration>
    {
        readonly Random _rnd;

        public D( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            _rnd = new Random( info.Configuration.RandomSeed );
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            return Task.CompletedTask;
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
        {
            return Task.FromResult( DeviceReconfiguredResult.None );
        }

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            return Task.FromResult( true );
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            return Task.CompletedTask;
        }

        protected override ValueTask<int> GetCommandTimeoutAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            if( command is DCommand cmd )
            {
                if( cmd.ExpectedCancellationReason == BaseDeviceCommand.CommandTimeoutReason )
                {
                    monitor.Trace( $"GetCommandTimeoutAsync set to 30 ms for {command}" );
                    return ValueTask.FromResult( 30 );
                }
            }
            return ValueTask.FromResult( 0 );
        }

        protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            if( command is DCommand cmd )
            {
                if( _rnd.Next( 2 ) == 0 )
                {
                    var t = DateTime.UtcNow.Add( cmd.WaitToComplete );
                    monitor.Trace( $"AddReminder to complete {command} in {(long)cmd.WaitToComplete.TotalMilliseconds} ms." );
                    AddReminder( t, cmd );
                }
                else
                {
                    monitor.Trace( $"Using delayed Task.Run() to complete {command} in {(long)cmd.WaitToComplete.TotalMilliseconds}." );
                    _ = Task.Run( async () =>
                    {
                        await Task.Delay( cmd.WaitToComplete );
                        DoComplete( cmd );
                        ActivityMonitor.StaticLogger.Trace( $"Task.Run() completed {command}." );
                    } );
                }
                return;
            }
            await base.DoHandleCommandAsync( monitor, command );
        }

        protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling )
        {
            var cmd = (DCommand)state!;
            monitor.Trace( $"OnReminderAsync for {cmd} (immediateHandling: {immediateHandling})." );
            DoComplete( (DCommand)state! );
            return Task.CompletedTask;
        }

        static void DoComplete( DCommand cmd )
        {
            if( cmd.ExplicitCancelOnComplete )
                cmd.Cancel( "ExplicitCancel" );
            else cmd.Completion.TrySetResult();
        }

        protected override Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            monitor.Trace( $"OnCommandCompletedAsync for {command}: CancellationReason: '{command.CancellationReason ?? "<Success>"}'" );
            return Task.CompletedTask;
        }
    }

    public class DCommand : DeviceCommand<DHost>
    {
        public readonly int Number;

        public DCommand( int number ) => Number = number;

        public string? Trace => $"n°{Number}-{ExpectedCancellationReason ?? "<Success>"}' (case: {Number % 11})";

        public string? ExpectedCancellationReason { get; set; }
        public TimeSpan WaitToComplete { get; set; }
        public bool ExplicitCancelOnComplete { get; set; }

        // Avoids exception while awaiting. Using Completion.HasBeenCanceled.
        protected override void OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetResult();

        protected override string? ToStringSuffix => Trace;
    }

    [TestCase( 11, 3713 )]
    [TestCase( 200, 42 )]
    [CancelAfter( 1200 )]
    public async Task multiple_cancellation_reasons_Async( int nb, int randomSeed, CancellationToken cancellation )
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( multiple_cancellation_reasons_Async )}({nb},{randomSeed})" );
        try
        {
            var rnd = new Random( randomSeed );
            var h = new DHost();
            var config = new DConfiguration() { Name = "Single", Status = DeviceConfigurationStatus.RunnableStarted, RandomSeed = rnd.Next() };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["Single"];
            Debug.Assert( d != null );

            var alreadyCanceled = new CancellationTokenSource();
            await alreadyCanceled.CancelAsync();

            var cancel200Timeout = new CancellationTokenSource( 200 );

            var neverCanceled = new CancellationTokenSource();

            var all = new List<DCommand>();

            for( int i = 0; i < nb; ++i )
            {
                CancellationTokenSource? sendCommandTimeout = null;

                var c = new DCommand( i );
                all.Add( c );
                // Each command will normally be completed in 500 ms but it starts
                // randomly (including immediately) and its completion is deferred either
                // by a reminder or by a stupid delayed Task.
                var startDelay = rnd.Next( 500 ) - 30;
                if( startDelay < 0 ) c.ImmediateSending = true;
                else c.SendingTimeUtc = DateTime.UtcNow.AddMilliseconds( startDelay );
                c.WaitToComplete = TimeSpan.FromMilliseconds( 500 - Math.Max( 0, startDelay ) );

                switch( i % 11 )
                {
                    case 0:
                        c.ExpectedCancellationReason = nameof( alreadyCanceled );
                        switch( i % 3 )
                        {
                            case 0:
                                c.AddCancellationSource( alreadyCanceled.Token, nameof( alreadyCanceled ) );
                                c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                                c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                                break;
                            case 1:
                                c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                                c.AddCancellationSource( alreadyCanceled.Token, nameof( alreadyCanceled ) );
                                c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                                break;
                            case 2:
                                c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                                c.AddCancellationSource( alreadyCanceled.Token, nameof( alreadyCanceled ) );
                                c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                                break;
                        }
                        break;
                    case 1:
                        c.ExpectedCancellationReason = nameof( cancel200Timeout );
                        switch( i % 2 )
                        {
                            case 0:
                                c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                                c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                                break;
                            case 1:
                                c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                                c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                                break;
                        }
                        break;
                    case 2:
                        c.ExpectedCancellationReason = nameof( alreadyCanceled );
                        c.AddCancellationSource( alreadyCanceled.Token, nameof( alreadyCanceled ) );
                        break;
                    case 3:
                        // SendCommandTokenReason or cancel200Timeout.
                        c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                        if( rnd.Next( 2 ) == 0 )
                        {
                            sendCommandTimeout = new CancellationTokenSource( 100 );
                            c.ExpectedCancellationReason = BaseDeviceCommand.SendCommandTokenReason;
                        }
                        else
                        {
                            c.ExpectedCancellationReason = nameof( cancel200Timeout );
                        }
                        break;
                    case 4:
                        // Success or SendCommandTokenReason.
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        if( rnd.Next( 2 ) == 0 )
                        {
                            sendCommandTimeout = new CancellationTokenSource( 300 );
                            c.ExpectedCancellationReason = BaseDeviceCommand.SendCommandTokenReason;
                        }
                        break;
                    case 5:
                        // CommandTimeoutReason occurs 50 ms after the DoHandleCommandAsync.
                        c.ExpectedCancellationReason = BaseDeviceCommand.CommandTimeoutReason;
                        break;
                    case 6:
                        c.ExpectedCancellationReason = BaseDeviceCommand.CommandTimeoutReason;
                        c.ImmediateSending = true;
                        c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        break;
                    case 7:
                        c.ExpectedCancellationReason = BaseDeviceCommand.CommandCompletionCanceledReason;
                        c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        c.Completion.SetCanceled();
                        break;
                    case 8:
                        c.ExpectedCancellationReason = "ExplicitCancel";
                        c.ExplicitCancelOnComplete = true;
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        break;
                    case 9:
                        c.ExpectedCancellationReason = BaseDeviceCommand.CommandTimeoutReason;
                        c.ExplicitCancelOnComplete = true;
                        c.ImmediateSending = true;
                        c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        break;
                    case 10:
                        c.ExpectedCancellationReason = BaseDeviceCommand.CommandCompletionCanceledReason;
                        c.AddCancellationSource( cancel200Timeout.Token, nameof( cancel200Timeout ) );
                        c.AddCancellationSource( neverCanceled.Token, nameof( neverCanceled ) );
                        _ = Task.Run( async () =>
                        {
                            await Task.Delay( 100 );
                            c.Completion.SetCanceled();
                        }, cancellation );
                        break;
                    default: Debug.Fail( "Never" ); break;
                }
                d.UnsafeSendCommand( TestHelper.Monitor, c, sendCommandTimeout?.Token ?? default );
            }
            // This is rather useless since in this test, completion is not the handling (long running commands).
            await d.WaitForSynchronizationAsync( false, cancel: cancellation );
            foreach( var c in all )
            {
                TestHelper.Monitor.Trace( $"Waiting for {c}." );
                await c.Completion;
                TestHelper.Monitor.Trace( $"Got {c}." );
                c.CancellationReason.Should().Be( c.ExpectedCancellationReason, c.ToString() );
            }

            await h.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        }
        catch( Exception ex )
        {
            TestHelper.Monitor.Fatal( ex );
            throw;
        }
    }
}
