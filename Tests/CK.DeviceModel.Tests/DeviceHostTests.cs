using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CK.Core;
using static CK.Testing.MonitorTestHelper;
using System.Diagnostics;
using FluentAssertions.Execution;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class DeviceHostTests
    {

        [Test]
        public async Task playing_with_configurations()
        {
            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

            var config1 = new CameraConfiguration() { Name = "First" };
            var config2 = new CameraConfiguration { Name = "Another", Status = DeviceConfigurationStatus.Runnable };
            var config3 = new CameraConfiguration { Name = "YetAnother", Status = DeviceConfigurationStatus.RunnableStarted };

            var host = new CameraHost();

            var hostConfig = new DeviceHostConfiguration<CameraConfiguration>();
            hostConfig.IsPartialConfiguration.Should().BeTrue( "By default a configuration is partial." );
            hostConfig.Items.Add( config1 );

            host.Count.Should().Be( 0 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 1 );
            Camera.TotalCount.Should().Be( 1 );

            hostConfig.Items.Add( config2 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 2 );
            Camera.TotalCount.Should().Be( 2 );
            Camera.TotalRunning.Should().Be( 0 );

            hostConfig.Items.Add( config3 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 3 );
            Camera.TotalCount.Should().Be( 3 );
            Camera.TotalRunning.Should().Be( 1 );

            Camera? c1 = host.Find( "First" );
            Camera? c2 = host.Find( "Another" );
            Camera? c3 = host.Find( "YetAnother" );
            host.Find( "Not here" ).Should().BeNull();
            Debug.Assert( c1 != null && c2 != null && c3 != null );

            c1.Name.Should().Be( "First" );
            c1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );
            c2.Name.Should().Be( "Another" );
            c2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );
            c3.Name.Should().Be( "YetAnother" );
            c3.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.RunnableStarted );

            c3.IsRunning.Should().BeTrue();
            (await c3.StopAsync( TestHelper.Monitor )).Should().BeTrue();
            Camera.TotalRunning.Should().Be( 0 );

            hostConfig.Items.Remove( config3 );
            hostConfig.Items.Remove( config1 );

            config1.Status = DeviceConfigurationStatus.AlwaysRunning;
            config2.Status = DeviceConfigurationStatus.AlwaysRunning;
            config3.Status = DeviceConfigurationStatus.AlwaysRunning;

            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 3 );

            c1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );
            c2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
            c3.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.RunnableStarted );

            hostConfig.IsPartialConfiguration = false;
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 1 );

            host.Find( "First" ).Should().BeNull();
            host.Find( "Another" ).Should().BeSameAs( c2 );
            host.Find( "YetAnother" ).Should().BeNull();

            await host.ClearAsync( TestHelper.Monitor );
            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

        }

        [Test]
        public async Task testing_state_changed_PerfectEvent()
        {
            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

            int devicesSyncCalled = 0;
            int devicesAsyncCalled = 0;
            DeviceStatus? lastSyncEvent = null;
            DeviceStatus? lastAsyncEvent = null;

            void DevicesChanged_Sync( IActivityMonitor monitor, IDeviceHost sender )
            {
                ++devicesSyncCalled;
            }

            Task DevicesChanged_Async( IActivityMonitor monitor, IDeviceHost sender )
            {
                ++devicesAsyncCalled;
                return Task.CompletedTask;
            }

            void StateChanged_Sync( IActivityMonitor monitor, IDevice sender )
            {
                lastSyncEvent = sender.Status;
            }

            Task StateChanged_Async( IActivityMonitor monitor, IDevice sender )
            {
                lastAsyncEvent = sender.Status;
                return Task.CompletedTask;
            }

            var host = new CameraHost();
            host.DevicesChanged.Sync += DevicesChanged_Sync;
            host.DevicesChanged.Async += DevicesChanged_Async;

            var config = new CameraConfiguration() { Name = "C" };
            var hostConfig = new DeviceHostConfiguration<CameraConfiguration>();
            hostConfig.Items.Add( config );

            var result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Success.Should().BeTrue();
            result.HostConfiguration.Should().BeSameAs( hostConfig );
            result.Results![0].Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            devicesSyncCalled.Should().Be( 1 );
            devicesAsyncCalled.Should().Be( 1 );

            var cameraC = host["C"];
            Debug.Assert( cameraC != null );
            cameraC.StatusChanged.Async += StateChanged_Async;
            cameraC.StatusChanged.Sync += StateChanged_Sync;
            lastSyncEvent.Should().BeNull();
            lastAsyncEvent.Should().BeNull();

            var resultNoChange = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            resultNoChange.Success.Should().BeTrue();
            resultNoChange.Results![0].Should().Be( DeviceApplyConfigurationResult.None );

            devicesSyncCalled.Should().Be( 1, "Still 1: no event raised." );
            devicesAsyncCalled.Should().Be( 1 );
            lastSyncEvent.Should().BeNull( "None doesn't raise." );
            lastAsyncEvent.Should().BeNull();

            config.FlashColor = 1;

            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Success.Should().BeTrue();
            result.Results![0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            devicesSyncCalled.Should().Be( 2 );
            devicesAsyncCalled.Should().Be( 2 );
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );

            lastSyncEvent.Value.HasStarted.Should().BeFalse();
            lastSyncEvent.Value.HasBeenReconfigured.Should().BeTrue();
            lastSyncEvent.Value.HasStopped.Should().BeFalse();
            lastSyncEvent.Value.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
            lastSyncEvent.ToString().Should().Be( "Stopped (UpdateSucceeded)" );

            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled." );
            cameraC.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );

            lastSyncEvent = null;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            lastSyncEvent.Should().BeNull( "No official change (DeviceReconfiguredResult.None returned by Device.DoReconfigureAsync) and no ConfigurationStatus nor ControllerKey change." );
            devicesSyncCalled.Should().Be( 2 );
            devicesAsyncCalled.Should().Be( 2 );

            // Changes the Configuration status. Nothing change except this Device.ConfigurationStatus...
            config.Status = DeviceConfigurationStatus.Runnable;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Results[0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            // The Status did not change but the ConfigurationStatus did.
            lastSyncEvent.ToString().Should().Be( "Stopped (UpdateSucceeded)" );
            cameraC.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );

            devicesSyncCalled.Should().Be( 3 );
            devicesAsyncCalled.Should().Be( 3 );

            var cAndConfig = host.FindWithConfiguration( "C" );
            Debug.Assert( cAndConfig != null );
            cAndConfig.Value.Configuration.Status.Should().Be( DeviceConfigurationStatus.Runnable, "Even if Device decided that nothing changed, the updated configuration is captured." );

            // Starting the camera triggers a Device event.
            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );

            lastSyncEvent.Value.HasStarted.Should().BeTrue();
            lastSyncEvent.Value.HasBeenReconfigured.Should().BeFalse();
            lastSyncEvent.Value.HasStopped.Should().BeFalse();
            lastSyncEvent.Value.StartedReason.Should().Be( DeviceStartedReason.StartedCall );

            // AutoDestroying.
            lastSyncEvent = null;
            await cameraC.TestAutoDestroy( TestHelper.Monitor );
            devicesSyncCalled.Should().Be( 4, "Device removed!" );
            devicesAsyncCalled.Should().Be( 4 );
            host.Find( "C" ).Should().BeNull();
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );
            lastSyncEvent.Value.HasStarted.Should().BeFalse();
            lastSyncEvent.Value.HasBeenReconfigured.Should().BeFalse();
            lastSyncEvent.Value.HasStopped.Should().BeTrue();
            lastSyncEvent.Value.StoppedReason.Should().Be( DeviceStoppedReason.Destroyed );

            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );
        }


        [Test]
        public async Task apply_device_configuration()
        {
            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

            var host = new CameraHost();
            var d = host.Find( "n°1" );
            d.Should().BeNull();

            var config = new CameraConfiguration()
            {
                Name = "n°1",
                FlashColor = 78
            };
            DeviceApplyConfigurationResult reconfigResult = await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config );
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
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            d.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
            d.Status.IsRunning.Should().BeTrue();
            d.Status.StartedReason.Should().Be( DeviceStartedReason.StartedByAlwaysRunningConfiguration );

            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.None, "No change: the Camera detects it." );

            config.ControllerKey = "Control";
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded, "Even if the specific configuration did not change, changing the ControllerKey is a change." );

            d.Status.StartedReason.Should().Be( DeviceStartedReason.None );
            d.Status.HasBeenReconfigured.Should().BeTrue();
            d.Status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
            d.ControllerKey.Should().Be( "Control" );

            config.Status = DeviceConfigurationStatus.Disabled;
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            d.Status.HasStopped.Should().BeTrue();
            d.Status.StoppedReason.Should().Be( DeviceStoppedReason.StoppedByDisabledConfiguration );

            await host.DestroyDeviceAsync( TestHelper.Monitor, "n°1" );

            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

            host.Awaiting( h => h.DestroyDeviceAsync( TestHelper.Monitor, "n°1" ) ).Should().NotThrow();

        }
    }
}
