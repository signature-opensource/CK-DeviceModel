using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CK.Core;
using System.Diagnostics;
using FluentAssertions.Execution;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class DeviceHostTests
    {

        [Test]
        public async Task playing_with_configurations()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( playing_with_configurations ) );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            var config1 = new FlashBulbConfiguration() { Name = "First" };
            var config2 = new FlashBulbConfiguration { Name = "Another", Status = DeviceConfigurationStatus.Runnable };
            var config3 = new FlashBulbConfiguration { Name = "YetAnother", Status = DeviceConfigurationStatus.RunnableStarted };

            var host = new FlashBulbHost();

            var hostConfig = new DeviceHostConfiguration<FlashBulbConfiguration>();
            hostConfig.IsPartialConfiguration.Should().BeTrue( "By default a configuration is partial." );
            hostConfig.Items.Add( config1 );

            host.Count.Should().Be( 0 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 1 );
            FlashBulb.TotalCount.Should().Be( 1 );

            hostConfig.Items.Add( config2 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 2 );
            FlashBulb.TotalCount.Should().Be( 2 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            hostConfig.Items.Add( config3 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 3 );
            FlashBulb.TotalCount.Should().Be( 3 );
            FlashBulb.TotalRunning.Should().Be( 1 );

            FlashBulb? c1 = host.Find( "First" );
            FlashBulb? c2 = host.Find( "Another" );
            FlashBulb? c3 = host.Find( "YetAnother" );
            host.Find( "Not here" ).Should().BeNull();
            Debug.Assert( c1 != null && c2 != null && c3 != null );

            c1.ExternalConfiguration.Should().NotBeSameAs( config1 );
            c2.ExternalConfiguration.Should().NotBeSameAs( config2 );
            c3.ExternalConfiguration.Should().NotBeSameAs( config3 );

            c1.Name.Should().Be( "First" );
            c1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
            c2.Name.Should().Be( "Another" );
            c2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable );
            c3.Name.Should().Be( "YetAnother" );
            c3.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.RunnableStarted );

            c3.IsRunning.Should().BeTrue();
            (await c3.StopAsync( TestHelper.Monitor )).Should().BeTrue();
            FlashBulb.TotalRunning.Should().Be( 0 );

            // Partial configuration here: leave only config2 (RunnableStarted).
            hostConfig.Items.Remove( config3 );
            hostConfig.Items.Remove( config1 );

            config1.Status = DeviceConfigurationStatus.AlwaysRunning;
            config2.Status = DeviceConfigurationStatus.AlwaysRunning;
            config3.Status = DeviceConfigurationStatus.AlwaysRunning;

            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 3 );

            c1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
            c2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
            c3.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.RunnableStarted );

            hostConfig.IsPartialConfiguration = false;
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 1 );

            host.Find( "First" ).Should().BeNull();
            host.Find( "Another" ).Should().BeSameAs( c2 );
            host.Find( "YetAnother" ).Should().BeNull();

            await host.ClearAsync( TestHelper.Monitor );
            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

        }

        [Test]
        public async Task testing_state_changed_PerfectEvent()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( testing_state_changed_PerfectEvent ) );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            int devicesCalled = 0;
            var lifetimeEvents = new System.Collections.Generic.List<DeviceLifetimeEvent>();

            var host = new FlashBulbHost();
            host.DevicesChanged.Sync += (monitor,host) => ++devicesCalled;

            var config = new FlashBulbConfiguration() { Name = "C" };
            var hostConfig = new DeviceHostConfiguration<FlashBulbConfiguration>();
            hostConfig.Items.Add( config );

            var result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Success.Should().BeTrue();
            result.HostConfiguration.Should().BeSameAs( hostConfig );
            result.Results![0].Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            devicesCalled.Should().Be( 1 );

            var cameraC = host["C"];
            Debug.Assert( cameraC != null );
            {
                var status = cameraC.Status;
                status.HasStarted.Should().BeFalse();
                status.HasBeenReconfigured.Should().BeFalse();
                status.HasStopped.Should().BeFalse();
                status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.None );
                status.StartedReason.Should().Be( DeviceStartedReason.None );
                status.StoppedReason.Should().Be( DeviceStartedReason.None );
                status.ToString().Should().Be( "Stopped (None)" );
            }
            cameraC.LifetimeEvent.Sync += ( m, e ) => lifetimeEvents.Add( e );

            var resultNoChange = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            resultNoChange.Success.Should().BeTrue();
            resultNoChange.Results![0].Should().Be( DeviceApplyConfigurationResult.None );

            devicesCalled.Should().Be( 1, "Still 1: no event raised." );
            lifetimeEvents.Should().BeEmpty( "None doesn't raise." );
            cameraC.Status.ToString().Should().Be( "Stopped (None)" );

            // Applying a new configuration.
            config.FlashColor = 1;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Success.Should().BeTrue();
            result.Results![0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            devicesCalled.Should().Be( 1, "No new or destroyed devices." );
            lifetimeEvents.Should().HaveCount( 2 );
            lifetimeEvents[0].Should().BeOfType<DeviceStatusChangedEvent>();
            {
                var status = ((DeviceStatusChangedEvent)lifetimeEvents[0]).Status;
                status.HasStarted.Should().BeFalse();
                status.HasBeenReconfigured.Should().BeTrue();
                status.HasStopped.Should().BeFalse();
                status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
                status.StartedReason.Should().Be( DeviceStartedReason.None );
                status.StoppedReason.Should().Be( DeviceStartedReason.None );
                status.ToString().Should().Be( "Stopped (UpdateSucceeded)" );
            }
            lifetimeEvents[1].Should().BeOfType<DeviceConfigurationChangedEvent>();
            {
                var c = ((DeviceConfigurationChangedEvent)lifetimeEvents[1]).Configuration;
                c.Should().NotBeSameAs( config ).And.BeEquivalentTo( config );
            }
            lifetimeEvents.Clear();

            // Try to start...
            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled." );
            cameraC.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
            lifetimeEvents.Should().BeEmpty();

            // No change.
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            lifetimeEvents.Should().BeEmpty();
            devicesCalled.Should().Be( 1 );

            // Changes the Configuration status. Nothing change except this Device.ConfigurationStatus...
            config.Status = DeviceConfigurationStatus.Runnable;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Results[0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            lifetimeEvents.Should().HaveCount( 1 );
            lifetimeEvents[0].Should().BeOfType<DeviceConfigurationChangedEvent>();
            cameraC.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable, "The Status has been updated." );
            devicesCalled.Should().Be( 1 );

            lifetimeEvents.Clear();
            // Starting the camera triggers a DeviceStatusChangedEvent event.
            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            lifetimeEvents.Should().HaveCount( 1 );
            lifetimeEvents[0].Should().BeOfType<DeviceStatusChangedEvent>();
            lifetimeEvents.Should().HaveCount( 1 );
            {
                var status = ((DeviceStatusChangedEvent)lifetimeEvents[0]).Status;
                status.HasStarted.Should().BeTrue();
                status.HasBeenReconfigured.Should().BeFalse();
                status.HasStopped.Should().BeFalse();
                status.StartedReason.Should().Be( DeviceStartedReason.StartCall );
                status.ToString().Should().Be( "Running (StartCall)" );
            }

            lifetimeEvents.Clear();
            // AutoDestroying by sending the command to host.
            var cmd = new DestroyDeviceCommand<FlashBulbHost>() { DeviceName = "C" };
            host.SendCommand( TestHelper.Monitor, cmd ).Should().Be( DeviceHostCommandResult.Success );
            await cmd.Completion.Task;

            devicesCalled.Should().Be( 2, "Device removed!" );
            host.Find( "C" ).Should().BeNull();
            lifetimeEvents.Should().HaveCount( 1 );
            lifetimeEvents[0].Should().BeOfType<DeviceStatusChangedEvent>();
            {
                var status = ((DeviceStatusChangedEvent)lifetimeEvents[0]).Status;
                status.HasStarted.Should().BeFalse();
                status.HasBeenReconfigured.Should().BeFalse();
                status.HasStopped.Should().BeTrue();
                status.StoppedReason.Should().Be( DeviceStoppedReason.Destroyed );
                status.ToString().Should().Be( "Stopped (Destroyed)" );
            }

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );
        }


        [Test]
        public async Task ensure_device()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( ensure_device ) );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            var host = new FlashBulbHost();
            var d = host.Find( "n°1" );
            d.Should().BeNull();

            var config = new FlashBulbConfiguration()
            {
                Name = "n°1",
                FlashColor = 78
            };
            DeviceApplyConfigurationResult reconfigResult = await host.EnsureDeviceAsync( TestHelper.Monitor, config );
            reconfigResult.Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            d = host.Find( "n°1" );
            Debug.Assert( d != null );
            d.Status.IsRunning.Should().BeFalse();
            d.Status.HasStopped.Should().BeFalse( "The device is not running... but it has not been stopped." );
            d.Status.StoppedReason.Should().Be( DeviceStoppedReason.None );
            d.Status.IsDestroyed.Should().BeFalse();
            d.Status.HasBeenReconfigured.Should().BeFalse();
            d.Status.HasStarted.Should().BeFalse();
            d.Status.StartedReason.Should().Be( DeviceStartedReason.None );
            d.Status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.None );

            config.Status = DeviceConfigurationStatus.AlwaysRunning;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            d.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
            d.Status.IsRunning.Should().BeTrue();
            d.Status.StartedReason.Should().Be( DeviceStartedReason.StartedByAlwaysRunningConfiguration );

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.None, "No change: the Camera detects it." );

            config.ControllerKey = "Control";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded, "Even if the specific configuration did not change, changing the ControllerKey is a change." );

            d.Status.StartedReason.Should().Be( DeviceStartedReason.None );
            d.Status.HasBeenReconfigured.Should().BeTrue();
            d.Status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
            d.ControllerKey.Should().Be( "Control" );

            config.Status = DeviceConfigurationStatus.Disabled;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            d.Status.HasStopped.Should().BeTrue();
            d.Status.StoppedReason.Should().Be( DeviceStoppedReason.StoppedByDisabledConfiguration );

            await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            d.Awaiting( _ => _.DestroyAsync( TestHelper.Monitor ) ).Should().NotThrow();
        }

        [Test]
        public async Task executing_commands_from_the_host_without_ControllerKey()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( executing_commands_from_the_host_without_ControllerKey ) );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            var host = new FlashBulbHost();
            var config = new FlashBulbConfiguration()
            {
                Name = "n°1",
                FlashColor = 78,
                Status = DeviceConfigurationStatus.RunnableStarted
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host.Find( "n°1" );
            Debug.Assert( d != null );

            int flashLastColor = 0;
            d.TestFlash.Sync += (m,c,color) => flashLastColor = color;

            var cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
            host.SendCommand( TestHelper.Monitor, cmdF ).Should().Be( DeviceHostCommandResult.Success );
            await cmdF.Completion.Task;

            flashLastColor.Should().Be( 78 );

            var cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
            host.SendCommand( TestHelper.Monitor, cmdS ).Should().Be( DeviceHostCommandResult.Success );
            await cmdS.Completion.Task;

            flashLastColor.Should().Be( 78 );
            cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
            host.SendCommand( TestHelper.Monitor, cmdF ).Should().Be( DeviceHostCommandResult.Success );
            await cmdF.Completion.Task;

            flashLastColor.Should().Be( 3712 );

            host.SendCommand( TestHelper.Monitor, cmdS ).Should().Be( DeviceHostCommandResult.CommandCheckValidityFailed );

            cmdS = new SetFlashColorCommand() { DeviceName = "Not the 1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
            host.SendCommand( TestHelper.Monitor, cmdS ).Should().Be( DeviceHostCommandResult.DeviceNameNotFound );

            await d.SetControllerKeyAsync( TestHelper.Monitor, null, "The controlling key." );
            cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Controller key will fail!", Color = 3712 };
            host.SendCommand( TestHelper.Monitor, cmdS ).Should().Be( DeviceHostCommandResult.Success );
            FluentActions.Awaiting( () => cmdS.Completion.Task ).Should().Throw<InvalidControllerKeyException>();

            cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "The controlling key.", Color = 3712 };
            host.SendCommand( TestHelper.Monitor, cmdS ).Should().Be( DeviceHostCommandResult.Success );

            await cmdS.Completion.Task;

            await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

        }

        [TestCase( "UseSendCommand" )]
        [TestCase( "UseSendCommandImmediate" )]
        public async Task sending_commands_checks_DeviceName_and_executing_checks_ControllerKey( string mode )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( sending_commands_checks_DeviceName_and_executing_checks_ControllerKey ) );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

            var host = new FlashBulbHost();
            var config = new FlashBulbConfiguration()
            {
                Name = "n°1",
                FlashColor = 78,
                Status = DeviceConfigurationStatus.RunnableStarted
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            FlashBulb? d = host.Find( "n°1" );
            Debug.Assert( d != null );

            bool SendCommand( BaseDeviceCommand c, bool checkDeviceName = true, bool checkControllerKey = true )
            {
                return mode == "UseSendCommandImmediate"
                        ? d.SendCommandImmediate( TestHelper.Monitor, c, checkDeviceName, checkControllerKey )
                        : d.SendCommand( TestHelper.Monitor, c, checkDeviceName, checkControllerKey );
            }

            int flashLastColor = 0;
            d.TestFlash.Sync += (m,c,color) => flashLastColor = color;

            var cmdSet = new SetFlashColorCommand()
            {
                DeviceName = "n°1",
                ControllerKey = "Never mind since the device's ControllerKey is null.",
                Color = 6
            };
            var cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1" };

            SendCommand( cmdSet );
            SendCommand( cmdRaiseFlash );

            (await cmdSet.Completion).Should().Be( 78 );
            await cmdRaiseFlash.Completion.Task;
            flashLastColor.Should().Be( 6 );

            // Use the basic command to set a ControllerKey.
            var setControllerKey = new SetControllerKeyDeviceCommand<FlashBulbHost>()
            {
                ControllerKey = "Never mind since the device's ControllerKey is null.",
                NewControllerKey = "I'm controlling.",
                DeviceName = "n°1"
            };
            SendCommand( setControllerKey );

            cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm not in charge. Completion will throw an InvalidControllerKeyException." };
            SendCommand( cmdSet ).Should().BeTrue();

            do
            {
                await Task.Delay( 100 );
            }
            while( !cmdSet.Completion.IsCompleted );

            cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling.", Color = 18 };
            SendCommand( cmdSet );

            cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1" };
            SendCommand( cmdRaiseFlash ).Should().BeTrue();
            FluentActions.Awaiting( () => cmdRaiseFlash.Completion.Task ).Should().Throw<InvalidControllerKeyException>();

            flashLastColor.Should().Be( 6 );

            cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling." };
            SendCommand( cmdRaiseFlash );

            await cmdRaiseFlash.Completion.Task;
            flashLastColor.Should().Be( 18 );

            cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling, but checkControllerKey: false is used.", Color = 1 };
            cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling too." };
            SendCommand( cmdSet, checkControllerKey: false );
            SendCommand( cmdRaiseFlash, checkControllerKey: false );
            await cmdRaiseFlash.Completion.Task;
            flashLastColor.Should().Be( 1 );

            cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling.", Color = 1 };
            FluentActions.Invoking( () => SendCommand( cmdSet ) ).Should().Throw<ArgumentException>();

            cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling." };
            cmdRaiseFlash.DeviceName = "Not the right device name: this will throw an ArgumentException.";
            FluentActions.Invoking( () => SendCommand( cmdRaiseFlash ) ).Should().Throw<ArgumentException>();

            cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name but checkDeviceName: false is used.", ControllerKey = "I'm controlling.", Color = 3712 };
            cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name too.", ControllerKey = "I'm controlling." };
            SendCommand( cmdSet, checkDeviceName: false );
            SendCommand( cmdRaiseFlash, checkDeviceName: false );
            await cmdRaiseFlash.Completion.Task;
            flashLastColor.Should().Be( 3712 );

            await d.DestroyAsync( TestHelper.Monitor );

            FlashBulb.TotalCount.Should().Be( 0 );
            FlashBulb.TotalRunning.Should().Be( 0 );

        }

        [Test]
        public async Task Disabling_sends_a_stop_status_change()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( Disabling_sends_a_stop_status_change ) );
            var host = new MachineHost();

            var config = new MachineConfiguration()
            {
                Name = "Test",
                Status = DeviceConfigurationStatus.AlwaysRunning
            };

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var device = host["Test"];
            Debug.Assert( device != null );

            bool stopReceived = false;
            bool destroyReceived = false;

            device.LifetimeEvent.Sync += ( monitor, e ) =>
            {
                if( e is DeviceStatusChangedEvent ev )
                {
                    // The device's status is up to date.
                    Debug.Assert( ev.Device.Status == ev.Status );
                    TestHelper.Monitor.Info( $"Status change." );
                    if( ev.Status.IsDestroyed )
                    {
                        destroyReceived.Should().BeFalse();
                        destroyReceived = true;
                    }
                    else if( ev.Status.HasStopped )
                    {
                        // HasStopped is true when IsDestroyed is sent.
                        stopReceived.Should().BeFalse();
                        stopReceived = true;
                    }
                }
            };

            config.Status = DeviceConfigurationStatus.Disabled;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            stopReceived.Should().BeTrue();
            destroyReceived.Should().BeFalse();

            await device.DestroyAsync( TestHelper.Monitor );
            destroyReceived.Should().BeTrue();
        }

    }
}
