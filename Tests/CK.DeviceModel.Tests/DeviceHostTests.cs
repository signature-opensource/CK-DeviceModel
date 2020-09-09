using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using static CK.Testing.MonitorTestHelper;
using System.Diagnostics;

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
            }
        }

        public class Camera : Device<CameraConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public Camera( IActivityMonitor monitor, CameraConfiguration config )
                : base( monitor, config )
            {
                Interlocked.Increment( ref TotalCount );
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config )
            {
                return Task.FromResult( DeviceReconfiguredResult.UpdateSucceeded );
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

            var config1 = new CameraConfiguration(){ Name = "First" };
            var config2 = new CameraConfiguration{ Name = "Another", Status = DeviceConfigurationStatus.Runnable };
            var config3 = new CameraConfiguration{ Name = "YetAnother", Status = DeviceConfigurationStatus.RunnableStarted };

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
    }
}
