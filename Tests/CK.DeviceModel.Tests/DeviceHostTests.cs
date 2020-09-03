using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class DeviceHostTests
    {

        public interface ICameraConfiguration : IDeviceConfiguration
        {
        }

        public class CameraConfiguration : ICameraConfiguration
        {
            public string Name { get; set; }

            public DeviceConfigurationStatus ConfigurationStatus { get; set; }

            public CameraConfiguration( string name )
            {
                Name = name;
            }

            public IDeviceConfiguration Clone() => new CameraConfiguration( Name ) { ConfigurationStatus = ConfigurationStatus };
        }

        public class Camera : Device<ICameraConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public Camera( IActivityMonitor monitor, ICameraConfiguration config )
                : base( monitor, config )
            {
                Interlocked.Increment( ref TotalCount );
            }

            protected override Task<DeviceReconfigurationResult> DoReconfigureAsync( IActivityMonitor monitor, ICameraConfiguration config )
            {
                return Task.FromResult( DeviceReconfigurationResult.UpdateSucceeded );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, bool fromConfiguration )
            {
                Interlocked.Increment( ref TotalRunning );
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, StoppedReason reason )
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

        public class CameraHost : DeviceHost<Camera,DeviceHostConfiguration<ICameraConfiguration>,ICameraConfiguration,CameraHost>
        {
        }

        [Test]
        public async Task playing_with_configurations()
        {
            Camera.TotalCount.Should().Be( 0 );
            Camera.TotalRunning.Should().Be( 0 );

            var config1 = new CameraConfiguration( "First" );
            var config2 = new CameraConfiguration( "Another" ) { ConfigurationStatus = DeviceConfigurationStatus.Runnable };
            var config3 = new CameraConfiguration( "YetAnother" ) { ConfigurationStatus = DeviceConfigurationStatus.RunnableStarted };

            var host = new CameraHost();

            var hostConfig = new DeviceHostConfiguration<ICameraConfiguration>();
            hostConfig.IsPartialConfiguration.Should().BeTrue( "By default a configuration is partial." );
            hostConfig.Configurations.Add( config1 );

            host.Count.Should().Be( 0 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 1 );
            Camera.TotalCount.Should().Be( 1 );

            hostConfig.Configurations.Add( config2 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 2 );
            Camera.TotalCount.Should().Be( 2 );
            Camera.TotalRunning.Should().Be( 0 );

            hostConfig.Configurations.Add( config3 );
            await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
            host.Count.Should().Be( 3 );
            Camera.TotalCount.Should().Be( 3 );
            Camera.TotalRunning.Should().Be( 1 );

            Camera c1 = host.Find( "First" );
            Camera c2 = host.Find( "Another" );
            Camera c3 = host.Find( "YetAnother" );
            host.Find( "Not here" ).Should().BeNull();

            c1.Name.Should().Be( "First" );
            c1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );
            c2.Name.Should().Be( "Another" );
            c2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );
            c3.Name.Should().Be( "YetAnother" );
            c3.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.RunnableStarted );

            c3.IsRunning.Should().BeTrue();
            (await c3.StopAsync( TestHelper.Monitor )).Should().BeTrue();
            Camera.TotalRunning.Should().Be( 0 );

            hostConfig.Configurations.Remove( config3 );
            hostConfig.Configurations.Remove( config1 );

            config1.ConfigurationStatus = DeviceConfigurationStatus.AlwaysRunning;
            config2.ConfigurationStatus = DeviceConfigurationStatus.AlwaysRunning;
            config3.ConfigurationStatus = DeviceConfigurationStatus.AlwaysRunning;

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

        public class BuggyCamera : Device<CameraConfiguration>
        {
            public BuggyCamera( IActivityMonitor monitor, CameraConfiguration config )
                : base( monitor, config )
            {
            }

            protected override Task<DeviceReconfigurationResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config )
            {
                return Task.FromResult( DeviceReconfigurationResult.UpdateSucceeded );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, bool fromConfiguration )
            {
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, StoppedReason reason )
            {
                return Task.CompletedTask;
            }
        }

        public class BuggyCameraHost : DeviceHost<BuggyCamera, DeviceHostConfiguration<CameraConfiguration>, CameraConfiguration, BuggyCameraHost>
        {
        }

        [Test]
        public void TConfiguration_must_be_an_interface()
        {
            Action c = () => new BuggyCameraHost();
            c.Invoking( x => x() ).Should().Throw<TypeLoadException>();
        }

    }
}
