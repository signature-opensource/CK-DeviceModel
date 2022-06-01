using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class CommandCompletionDelayedByReminderTests
    {

        public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
        {
        }

        public class DConfiguration : DeviceConfiguration
        {
            public DConfiguration()
            {
            }

            public int RandomSeed { get; set; }

            public DConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte();
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
            }
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
            }

            public int OnCommandCompletionErrorCount;
            public int OnCommandCompletionCancelCount;
            public int OnCommandCompletionSuccessCount;
            Random _rnd = new Random();

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

            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    AddReminder( TimeSpan.FromMilliseconds( 10 + _rnd.Next( 100 ) ), cmd );
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command );
            }

            protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state )
            {
                switch( _rnd.Next( 3 ) )
                {
                    case 0: ((DCommand)state!).Completion.SetCanceled(); break;
                    case 1: ((DCommand)state!).Completion.SetResult(); break;
                    case 2: ((DCommand)state!).Completion.SetException( new Exception() ); break;
                }
                return Task.CompletedTask;
            }

            protected override Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    if( cmd.Completion.HasFailed ) ++OnCommandCompletionErrorCount;
                    else if( cmd.Completion.HasBeenCanceled ) ++OnCommandCompletionCancelCount;
                    else ++OnCommandCompletionSuccessCount;
                }
                return Task.CompletedTask;
            }

        }

        public class DCommand : DeviceCommand<DHost>
        {
            public string? Trace { get; set; }

            protected override string? ToStringSuffix => Trace;
        }

        [TestCase( 20, 3712 )]
        [TestCase( 500, 42 )]
        [Timeout( 3000 )]
        public async Task completion_with_reminder_Async( int nb, int randomSeed )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( completion_with_reminder_Async )}({nb},{randomSeed})" );
            var h = new DHost();
            var config = new DConfiguration() { Name = "Single", Status = DeviceConfigurationStatus.RunnableStarted, RandomSeed = randomSeed };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["Single"];
            Debug.Assert( d != null );

            var l = Enumerable.Range( 0, nb ).Select( _ => new DCommand() ).ToList();
            foreach( var c in l ) d.UnsafeSendCommand( TestHelper.Monitor, c );

            int nbError = 0;
            int nbCancel = 0;
            int nbSuccess = 0;
            foreach( var c in l )
            {
                try
                {
                    await c.Completion;
                    ++nbSuccess;
                }
                catch( Exception )
                {
                    if( c.Completion.HasBeenCanceled ) ++nbCancel;
                    else ++nbError;
                }
            }
            // Completion is signaled and then OnCommandCompletedAsync is called.
            // We have to wait.
            // Instead of waiting for a delay, use the new WaitForSynchronizationAsync method.
            //   await Task.Delay( nb * 2 );
            (await d.WaitForSynchronizationAsync( true )).Should().Be( WaitForSynchronizationResult.Success );

            d.OnCommandCompletionErrorCount.Should().Be( nbError );
            d.OnCommandCompletionCancelCount.Should().Be( nbCancel );
            d.OnCommandCompletionSuccessCount.Should().Be( nbSuccess );
        }
    }
}
