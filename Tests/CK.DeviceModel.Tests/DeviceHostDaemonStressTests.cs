using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class DeviceHostDaemonStressTests
    {

        public class AlwaysRetryPolicy : IDeviceAlwaysRunningPolicy
        {
            readonly int _retryTime;

            public AlwaysRetryPolicy( int retryTime )
            {
                _retryTime = retryTime;
            }

            public async Task<int> RetryStartAsync( IActivityMonitor monitor, IDeviceHost host, IDevice device, int retryCount )
            {
                if( await device.StartAsync( monitor ) )
                {
                    return 0;
                }
                return _retryTime;
            }
        }

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
                Fail = r.ReadEnum<FailureType>();
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.WriteEnum( Fail );
            }

            public FailureType Fail { get; set; }

        }

        public enum FailureType
        {
            None,
            StartSync,
            StartAsync,
            CommandSync,
            CommandAsync
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Traces = new List<string>();
                Fail = info.Configuration.Fail;
            }

            public List<string> Traces { get; }

            // Settable so that we change this directly on the device (this is bad but
            // this avoids the reconfiguration command side-effects - however this is also tested).
            public FailureType Fail { get; set; }

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                Traces.Add( $"Destroy" );
                return Task.CompletedTask;
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
            {
                Traces.Add( $"Reconfigure" );
                Fail = config.Fail;
                return Task.FromResult( DeviceReconfiguredResult.None );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
            {
                Traces.Add( $"Start {reason}" );
                if( Fail == FailureType.StartSync ) throw new CKException( "Sync." );
                else if( Fail == FailureType.StartAsync ) return FailAsync();
                return Task.FromResult( true );
            }

            async Task<bool> FailAsync()
            {
                await Task.Delay( 1 );
                throw new CKException( "Async." );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
            {
                Traces.Add( $"Stop {reason}" );
                return Task.CompletedTask;
            }

            protected override Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    Traces.Add( $"Command {cmd.Trace}" );
                    if( Fail == FailureType.CommandSync ) throw new CKException( "Sync." );
                    else if( Fail == FailureType.CommandAsync ) return FailAsync();
                    cmd.Completion.SetResult( CommandResult.Success );
                    return Task.CompletedTask;
                }
                return base.DoHandleCommandAsync( monitor, command );
            }
        }

        public enum CommandResult
        {
            Success,
            Cancel,
            Failure
        }

        public class DCommand : DeviceCommand<DHost, CommandResult>
        {
            public string? Trace { get; set; }

            public int ExecutionTime { get; set; }

            protected override string? ToStringSuffix => Trace;

            // Using result rewriting to avoid try/catch in tests.
            protected override void OnCanceled( ref CompletionSource<CommandResult>.OnCanceled result ) => result.SetResult( CommandResult.Cancel );

            protected override void OnError( Exception ex, ref CompletionSource<CommandResult>.OnError result ) => result.SetResult( CommandResult.Failure );
        }


        [TestCase( 20, 3712, true )]
        [TestCase( 20, 3712, false )]
        [TestCase( 20, 42, true )]
        [TestCase( 40, 587, true )]
        [TestCase( 60, 0, true )]
        [Timeout( 3500 )]
        public async Task stress_test_Async( int nbDevice, int randomSeed, bool useDirectReconfiguration )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( stress_test_Async )}({nbDevice},{randomSeed},{useDirectReconfiguration})" );

            var rnd = randomSeed != 0 ? new Random( randomSeed ) : new Random();
            var policy = new AlwaysRetryPolicy( rnd.Next( 5 ) + 1 );
            var host = new DHost();
            var daemon = new DeviceHostDaemon( new IDeviceHost[] { host }, policy );

            int nbStartFailure = 0;
            int nbCommandFailure = 0;
            await ((IHostedService)daemon).StartAsync( default );
            var config = new DeviceHostConfiguration<DConfiguration>();
            for( int i = 0; i < nbDevice; i++ )
            {
                var fail = (FailureType)rnd.Next( 5 );
                if( fail == FailureType.StartSync || fail == FailureType.StartAsync ) ++nbStartFailure;
                else if( fail == FailureType.CommandSync || fail == FailureType.CommandAsync ) ++nbCommandFailure;

                config.Items.Add( new DConfiguration()
                {
                    Name = $"n°{i}-{fail}",
                    Status = DeviceConfigurationStatus.AlwaysRunning,
                    Fail = fail
                } );
            }
            TestHelper.Monitor.Info( $"{nbDevice} devices: StartFailure = {nbStartFailure}, CommandFailure = {nbCommandFailure}." );
            var applyResult = await host.ApplyConfigurationAsync( TestHelper.Monitor, config );
            applyResult.Success.Should().BeFalse();
            foreach( var r in applyResult.Results )
            {
                r.Should().Match( r => r == DeviceApplyConfigurationResult.CreateAndStartSucceeded
                                       || r == DeviceApplyConfigurationResult.CreateSucceededButStartFailed );
            }
            TestHelper.Monitor.Info( "Sending one command to each devices." ); 
            var devices = host.GetDevices().Values.ToArray();
            var commands = Enumerable.Range( 0, nbDevice )
                                     .Select( i => new DCommand() { Trace = $"n°{i}" } )
                                     .ToArray();
            for( int i = 0; i < nbDevice; i++ )
            {
                devices[i].UnsafeSendCommand( TestHelper.Monitor, commands[i] );
            }
            var deferred = new List<DCommand>();
            using( TestHelper.Monitor.OpenInfo( "Waiting for commands completion that can be completed." ) )
            {
                for( int i = 0; i < nbDevice; i++ )
                {
                    var d = devices[i];
                    var c = commands[i];
                    switch( d.Fail )
                    {
                        case FailureType.None:
                            (await c.Completion).Should().Be( CommandResult.Success, $"{c.Trace} should have succeeded." );
                            break;
                        case FailureType.CommandSync:
                        case FailureType.CommandAsync:
                            (await c.Completion).Should().Be( CommandResult.Failure, $"{c.Trace} should have failed." );
                            break;
                        case FailureType.StartSync:
                        case FailureType.StartAsync:
                            c.Completion.IsCompleted.Should().BeFalse( $"Command {c} ({c.Trace}) should be in the deferred queue and not completed yet." );
                            deferred.Add( c );
                            break;
                        default: throw new NotSupportedException();
                    }
                }
                TestHelper.Monitor.CloseGroup( $"{deferred.Count} commands deferred." );
            }
            // Let the daemon try to restart the failed devices (keeping the start error).
            await Task.Delay( 200 );

            using( TestHelper.Monitor.OpenInfo( "Reconfiguring the devices so that they all can now start without errors." ) )
            {
                if( useDirectReconfiguration )
                {
                    foreach( var d in devices )
                    {
                        if( d.Fail == FailureType.StartSync ) d.Fail = FailureType.CommandSync;
                        else if( d.Fail == FailureType.StartAsync ) d.Fail = FailureType.CommandAsync;
                    }
                    int w = 100 + nbDevice * 10;
                    TestHelper.Monitor.Info( $"Wait for the direct reconfiguration to be applied for {w} ms." );
                    // (There is no real way to do otherwise.)
                    await Task.Delay( w );
                }
                else
                {
                    foreach( var c in config.Items )
                    {
                        if( c.Fail == FailureType.StartSync ) c.Fail = FailureType.CommandSync;
                        else if( c.Fail == FailureType.StartAsync ) c.Fail = FailureType.CommandAsync;
                    }
                    applyResult = await host.ApplyConfigurationAsync( TestHelper.Monitor, config );
                    applyResult.Success.Should().BeTrue();
                }
            }
            TestHelper.Monitor.Info( $"{deferred.Count} deferred commands have now failed miserably (and stopped their device)..." );
            foreach( var c in deferred )
            {
                (await c.Completion).Should().Be( CommandResult.Failure );
            }
            TestHelper.Monitor.Info( $"Let the daemon try to restart the stopped devices during 200 ms." );
            await Task.Delay( 200 );

            using( TestHelper.Monitor.OpenInfo( "Reconfiguring the devices so that they all can now handle commands." ) )
            {
                if( useDirectReconfiguration )
                {
                    foreach( var d in devices )
                    {
                        d.Fail = FailureType.None;
                    }
                    int w = 100 + nbDevice * 10;
                    TestHelper.Monitor.Info( $"Wait for the direct reconfiguration to be applied for {w} ms." );
                    await Task.Delay( w );
                }
                else
                {
                    foreach( var c in config.Items )
                    {
                        c.Fail = FailureType.None;
                    }
                    applyResult = await host.ApplyConfigurationAsync( TestHelper.Monitor, config );
                    applyResult.Success.Should().BeTrue();
                }
            }

            var stopped = devices.Where( d => !d.IsRunning ).Select( d => d.Name ).ToArray();
            if( stopped.Length > 0 )
            {
                TestHelper.Monitor.Fatal( $"Still stopped devices are: {stopped.Concatenate()}." );
            }
            stopped.Should().BeEmpty();

            using( TestHelper.Monitor.OpenInfo( "Resending a bunch of commands." ) )
            {
                commands = Enumerable.Range( 0, nbDevice )
                        .Select( i => new DCommand() { Trace = $"n°{i} (bis)" } )
                        .ToArray();
                for( int i = 0; i < nbDevice; i++ )
                {
                    devices[i].UnsafeSendCommand( TestHelper.Monitor, commands[i] );
                }
            }

            using( TestHelper.Monitor.OpenInfo( "Waiting for their successful completion (all devices are running)." ) )
            {
                for( int i = 0; i < nbDevice; i++ )
                {
                    (await commands[i].Completion).Should().Be( CommandResult.Success );
                }
            }
            TestHelper.Monitor.Info( $"Destroying host and daemon." );
            await host.ClearAsync(TestHelper.Monitor, true );
            await ((IHostedService)daemon).StopAsync( default );
        }
    }
}
