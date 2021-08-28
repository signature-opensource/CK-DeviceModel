using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class AutoStartCommandAndDestroyTests
    {
        public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
        {
        }

        public class DConfiguration : DeviceConfiguration
        {
            public DConfiguration()
            {
            }

            public string? Trace { get; set; }

            public DConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte(); // version
                Trace = r.ReadNullableString();
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.WriteNullableString( Trace );
            }

        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Traces = new List<string>();
            }

            public List<string> Traces { get; }

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                Traces.Add( $"Destroy" );
                return Task.CompletedTask;
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
            {
                Traces.Add( $"Reconfigure {config.Trace}" );
                return Task.FromResult( DeviceReconfiguredResult.None );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
            {
                Traces.Add( $"Start {reason}" );
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
            {
                Traces.Add( $"Stop {reason}" );
                return Task.CompletedTask;
            }

            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
            {
                if( command is DCommand cmd )
                {
                    await Task.Delay( 10 );
                    Traces.Add( $"Command {cmd.Trace}" );
                    monitor.Info( $"Handling {cmd.Trace}" );
                    cmd.Completion.SetResult();
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command, token );
            }
        }


        public class DCommand : DeviceCommand<DHost>
        {
            public string? Trace { get; set; }

            protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.AlwaysWaitForNextStart;
        }

        public class DCommandStarter : DCommand
        {
            readonly DeviceCommandStoppedBehavior _stoppedBehavior;
            public DCommandStarter( bool keepDeviceRunning )
            {
                _stoppedBehavior = keepDeviceRunning ? DeviceCommandStoppedBehavior.AutoStartAndKeepRunning : DeviceCommandStoppedBehavior.SilentAutoStartAndStop;
            }

            protected override DeviceCommandStoppedBehavior StoppedBehavior => _stoppedBehavior;
        }

        [Test]
        public async Task auto_starting_device_and_keep_running_eventually_execute_deferred_commands()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( auto_starting_device_and_keep_running_eventually_execute_deferred_commands ) );

            var h = new DHost();
            var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );
            D? d = h["First"];
            Debug.Assert( d != null );

            bool statusChanged = false;
            void OnLifetimeChange( IActivityMonitor monitor, DeviceLifetimeEvent e ) => statusChanged |= e is DeviceStatusChangedEvent;
            d.LifetimeEvent.Sync += OnLifetimeChange;

            var commands = Enumerable.Range( 0, 3 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
            var destroy = new DestroyDeviceCommand<DHost>() { DeviceName = "First", ImmediateSending = false };

            foreach( var c in commands )
            {
                h.SendCommand( TestHelper.Monitor, c );
            }
            h.SendCommand( TestHelper.Monitor, new DCommandStarter( keepDeviceRunning: true ) { DeviceName = "First", Trace = "STARTER!" } );

            // Sending Destroy as a regular command (not an immediate one).
            h.SendCommand( TestHelper.Monitor, destroy );

            await commands[0].Completion.Task;
            statusChanged.Should().BeTrue( "Since the first deferred command is executed, the device has started." );

            await destroy.Completion;

            foreach( var c in commands )
            {
                c.Completion.IsCompleted.Should().BeTrue();
                c.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();
            }

            d.Traces.Should().BeEquivalentTo( "Start StartAndKeepRunningStoppedBehavior",
                                             "Command STARTER!",
                                             "Command n°0", "Command n°1", "Command n°2",
                                             "Stop Destroyed",
                                             "Destroy" );
        }

        [Test]
        public async Task auto_starting_device_and_stop_skip_deferred_and_no_event_is_raised()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( auto_starting_device_and_stop_skip_deferred_and_no_event_is_raised ) );

            var h = new DHost();
            var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );
            D? d = h["First"];
            Debug.Assert( d != null );

            var initialStatus = d.Status.ToString();
            initialStatus.Should().Be( "Stopped (None)" );

            bool statusChanged = false;
            void OnLifetimeChange( IActivityMonitor monitor, DeviceLifetimeEvent e ) => statusChanged |= e is DeviceStatusChangedEvent;
            d.LifetimeEvent.Sync += OnLifetimeChange;

            var commands = Enumerable.Range( 0, 3 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
            var starter = new DCommandStarter( keepDeviceRunning: false ) { DeviceName = "First", Trace = "DO IT" };
            var destroy = new DestroyDeviceCommand<DHost>() { DeviceName = "First" };

            foreach( var c in commands )
            {
                h.SendCommand( TestHelper.Monitor, c );
            }
            h.SendCommand( TestHelper.Monitor, starter );

            await starter.Completion.Task;
            // To let the implicit Stop command do its work.
            await Task.Delay( 50 );

            statusChanged.Should().BeFalse();
            d.Status.ToString().Should().Be( initialStatus );

            h.SendCommand( TestHelper.Monitor, destroy );

            await destroy.Completion.Task;

            foreach( var c in commands )
            {
                c.Completion.Task.IsCompleted.Should().BeFalse();
                c.Completion.IsCompleted.Should().BeFalse();
            }

            d.Traces.Should().BeEquivalentTo( "Start SilentAutoStartAndStopStoppedBehavior",
                                              "Command DO IT",
                                              "Stop SilentAutoStartAndStopStoppedBehavior",
                                              "Destroy" );
        }

        [TestCase( "Deferred" )]
        [TestCase( "StartFirst" )]
        public async Task destroying_the_device_eventually_set_the_UnavailableDeviceException_on_all_pending_commands( string mode )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( destroying_the_device_eventually_set_the_UnavailableDeviceException_on_all_pending_commands )}-{mode}" );

            var h = new DHost();
            var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );
            D? d = h["First"];
            Debug.Assert( d != null );

            var commands = Enumerable.Range( 0, 10 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
            if( mode == "StartFirst" ) (await d.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            foreach( var c in commands )
            {
                h.SendCommand( TestHelper.Monitor, c );
            }
            if( mode != "StartFirst" ) (await d.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            
            // Immediately destroys the device.
            await d.DestroyAsync( TestHelper.Monitor );

            TestHelper.Monitor.Info( $"Current Completions: {commands.Select( cz => cz.Completion.ToString() ).Concatenate()}." );
            foreach( var c in commands )
            {
                try
                {
                    await c.Completion;
                    // This is expected.
                }
                catch( UnavailableDeviceException ex )
                {
                    // This is expected.
                }
                c.Completion.IsCompleted.Should().BeTrue();
            }

            commands.Any( c => c.Completion.HasFailed ).Should().BeTrue( "At least one command should be true." );
        }


    }
}
