using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
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

        public class CameraConfiguration : DeviceConfiguration
        {
            public CameraConfiguration()
            {
            }

            public CameraConfiguration( CameraConfiguration o )
                : base( o )
            {
                Something = o.Something;
            }

            /// <summary>
            /// Used as a key to detect "equality".
            /// </summary>
            public int Something { get; set; }
        }

        public class Camera : Device<CameraConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            // A device can keep a reference to the current configuration:
            // this configuration is an independent clone that is accessible only to the Device.
            CameraConfiguration _configRef;

            public Camera( IActivityMonitor monitor, CameraConfiguration config )
                : base( monitor, config )
            {
                Interlocked.Increment( ref TotalCount );
                _configRef = config;
            }

            public Task TestAutoDestroy( IActivityMonitor monitor ) => AutoDestroyAsync( monitor );
            
            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config, bool controllerKeyChanged )
            {
                bool configHasChanged = config.Something != _configRef.Something;
                _configRef = config;
                return Task.FromResult( configHasChanged ? DeviceReconfiguredResult.UpdateSucceeded : DeviceReconfiguredResult.None );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
            {
                Interlocked.Increment( ref TotalRunning );
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
            {
                Interlocked.Decrement( ref TotalRunning );
                return Task.CompletedTask;
            }

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                Interlocked.Decrement( ref TotalCount );
                return Task.CompletedTask;
            }
        }

        public class CameraHost : DeviceHost<Camera,DeviceHostConfiguration<CameraConfiguration>,CameraConfiguration>
        {
        }

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

            void DevicesChanged_Sync( IActivityMonitor monitor, IDeviceHost sender, EventArgs e )
            {
                ++devicesSyncCalled;
            }

            Task DevicesChanged_Async( IActivityMonitor monitor, IDeviceHost sender, EventArgs e )
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

            config.Something = 1;

            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            result.Success.Should().BeTrue();
            result.Results![0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            devicesSyncCalled.Should().Be( 2 );
            devicesAsyncCalled.Should().Be( 2 );
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );

            lastSyncEvent.Value.IsStarted.Should().BeFalse();
            lastSyncEvent.Value.IsReconfigured.Should().BeTrue();
            lastSyncEvent.Value.IsStopped.Should().BeFalse();
            lastSyncEvent.Value.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
            lastSyncEvent.ToString().Should().Be( "Stopped (UpdateSucceeded)" );

            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled." );
            cameraC.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );

            lastSyncEvent = null;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            lastSyncEvent.Should().BeNull( "No official change (DeviceReconfiguredResult.None returned by Device.DoReconfigureAsync) and no ConfigurationStatus change." );
            devicesSyncCalled.Should().Be( 2 );
            devicesAsyncCalled.Should().Be( 2 );

            // Changes the Configuration status. Nothing change except this Device.ConfigurationStatus...
            config.Status = DeviceConfigurationStatus.Runnable;
            result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );

            // The Status did not change but the ConfigurationStatus did.
            lastSyncEvent.ToString().Should().Be( "Stopped (UpdateSucceeded)" );
            cameraC.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );

            devicesSyncCalled.Should().Be( 2 );
            devicesAsyncCalled.Should().Be( 2 );

            var cAndConfig = host.FindWithConfiguration( "C" );
            Debug.Assert( cAndConfig != null );
            cAndConfig.Value.Configuration.Status.Should().Be( DeviceConfigurationStatus.Runnable, "Even if Device decided that nothing changed, the updated configuration is captured." );

            // Starting the camera triggers a Device event.
            (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );

            lastSyncEvent.Value.IsStarted.Should().BeTrue();
            lastSyncEvent.Value.IsReconfigured.Should().BeFalse();
            lastSyncEvent.Value.IsStopped.Should().BeFalse();
            lastSyncEvent.Value.StartedReason.Should().Be( DeviceStartedReason.StartedCall );

            // AutoDestroying.
            lastSyncEvent = null;
            await cameraC.TestAutoDestroy( TestHelper.Monitor );
            devicesSyncCalled.Should().Be( 3, "Device removed!" );
            devicesAsyncCalled.Should().Be( 3 );
            host.Find( "C" ).Should().BeNull();
            Debug.Assert( lastSyncEvent != null && lastSyncEvent.Equals( lastAsyncEvent ) );
            lastSyncEvent.Value.IsStarted.Should().BeFalse();
            lastSyncEvent.Value.IsReconfigured.Should().BeFalse();
            lastSyncEvent.Value.IsStopped.Should().BeTrue();
            lastSyncEvent.Value.StoppedReason.Should().Be( DeviceStoppedReason.Destroyed );

            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );
        }

    }
}
