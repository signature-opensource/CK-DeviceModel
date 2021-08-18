using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Configuration.Tests
{
    [TestFixture]
    public class DynamicReconfigurationTests
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

            public CameraConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte(); // version
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
            }
        }

        public class Camera : Device<CameraConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public Camera( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
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

        public class CameraHost : DeviceHost<Camera, DeviceHostConfiguration<CameraConfiguration>, CameraConfiguration>
        {
            public static CameraHost? TestInstance;

            public CameraHost()
            {
                TestInstance = this;
            }
        }

        public class LightControllerConfiguration : DeviceConfiguration
        {
            public LightControllerConfiguration()
            {
            }

            public LightControllerConfiguration( LightControllerConfiguration o )
                : base( o )
            {
            }

            public LightControllerConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte(); // version
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
            }
        }

        public class LightController : Device<LightControllerConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public LightController( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Interlocked.Increment( ref TotalCount );
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, LightControllerConfiguration config )
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

        public class LightControllerHost : DeviceHost<LightController, DeviceHostConfiguration<LightControllerConfiguration>, LightControllerConfiguration>
        {
            public static LightControllerHost? Instance;

            public LightControllerHost()
            {
                Instance = this;
            }
        }

        class DynamicConfigurationSource : IConfigurationSource
        {
            public IConfigurationProvider Build( IConfigurationBuilder builder ) => new DynamicConfigurationProvider();
        }

        class DynamicConfigurationProvider : ConfigurationProvider
        {
            public void Remove( string path )
            {
                var keys = Data.Keys.Where( k => k == path || k.Length > path.Length && k.StartsWith( path ) && k[path.Length] == ConfigurationPath.KeyDelimiter[0] ).ToList();
                foreach( var k in keys ) Data.Remove( k );
            }

            public void RaiseChanged() => OnReload();
        }

        readonly struct DynamicConfiguration
        {
            public readonly IConfigurationRoot Root;

            public readonly DynamicConfigurationProvider Provider;

            DynamicConfiguration( IConfigurationRoot r )
            {
                Root = r;
                Provider = r.Providers.OfType<DynamicConfigurationProvider>().Single();
            }

            public static DynamicConfiguration Create() => new DynamicConfiguration( new ConfigurationBuilder().Add( new DynamicConfigurationSource() ).Build() );
        }

        [Test]
        public async Task empty_configuration_does_not_create_any_device()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( empty_configuration_does_not_create_any_device ) );

            var config = DynamicConfiguration.Create();
            await RunHost( config, services =>
            {
                Camera.TotalCount.Should().Be( 0 );
                Camera.TotalRunning.Should().Be( 0 );
                LightController.TotalCount.Should().Be( 0 );
                LightController.TotalRunning.Should().Be( 0 );
                return Task.CompletedTask;
            } );
        }

        [Test]
        public async Task initial_configuration_create_devices()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( initial_configuration_create_devices ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C1:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C2:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:LightControllerHost:Items:L1:Status", "Disabled" );
            await RunHost( config, services =>
            {
                Debug.Assert( CameraHost.TestInstance != null );
                Debug.Assert( LightControllerHost.Instance != null );

                Camera.TotalCount.Should().Be( 2 );
                Camera.TotalRunning.Should().Be( 0 );
                LightController.TotalCount.Should().Be( 1 );
                LightController.TotalRunning.Should().Be( 0 );
                var c1 = CameraHost.TestInstance.GetConfiguredDevice( "C1" );
                var c2 = CameraHost.TestInstance.GetConfiguredDevice( "C2" );
                var l1 = LightControllerHost.Instance.GetConfiguredDevice( "L1" );
                Debug.Assert( c1 != null && c2 != null && l1 != null );
                c1.Value.Configuration.Status.Should().Be( DeviceConfigurationStatus.Runnable );
                c2.Value.Configuration.Status.Should().Be( DeviceConfigurationStatus.Runnable );
                l1.Value.Configuration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
                return Task.CompletedTask;
            } );
        }

        [Test]
        public async Task initial_configuration_can_start_devices_and_then_they_live_their_lifes()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( initial_configuration_can_start_devices_and_then_they_live_their_lifes ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C1:Status", "RunnableStarted" );
            config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C2:Status", "AlwaysRunning" );
            config.Provider.Set( "CK-DeviceModel:LightControllerHost:Items:L1:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:LightControllerHost:Items:L2:Status", "Disabled" );
            await RunHost( config, async services =>
            {
                Debug.Assert( CameraHost.TestInstance != null );
                Debug.Assert( LightControllerHost.Instance != null );

                Camera.TotalCount.Should().Be( 2 );
                Camera.TotalRunning.Should().Be( 2 );
                LightController.TotalCount.Should().Be( 2 );
                LightController.TotalRunning.Should().Be( 0 );
                var c1 = CameraHost.TestInstance.Find( "C1" );
                var c2 = CameraHost.TestInstance.Find( "C2" );
                var l1 = LightControllerHost.Instance.Find( "L1" );
                var l2 = LightControllerHost.Instance.Find( "L2" );
                Debug.Assert( c1 != null && c2 != null && l1 != null && l2 != null );

                c1.IsRunning.Should().BeTrue();
                c1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.RunnableStarted );

                c2.IsRunning.Should().BeTrue();
                c2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.AlwaysRunning );

                l1.IsRunning.Should().BeFalse();
                l1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );

                l2.IsRunning.Should().BeFalse();
                l2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );

                (await c1.StopAsync( TestHelper.Monitor )).Should().BeTrue();
                c1.IsRunning.Should().BeFalse();
                c1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.RunnableStarted );
                (await c1.StopAsync( TestHelper.Monitor )).Should().BeTrue( "One can always stop an already stopped device." );

                (await c2.StopAsync( TestHelper.Monitor )).Should().BeFalse( "c2 is AlwaysRunning." );
                c2.IsRunning.Should().BeTrue();
                c2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.AlwaysRunning );

                (await l1.StartAsync( TestHelper.Monitor )).Should().BeTrue();
                l1.IsRunning.Should().BeTrue();
                l1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Runnable );

                (await l2.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled!" );
                l2.IsRunning.Should().BeFalse();
                l2.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );

                c1.IsDestroyed.Should().BeFalse();
                c2.IsDestroyed.Should().BeFalse();
                l1.IsDestroyed.Should().BeFalse();
                l2.IsDestroyed.Should().BeFalse();

                await CameraHost.TestInstance.ClearAsync( TestHelper.Monitor );
                await LightControllerHost.Instance.ClearAsync( TestHelper.Monitor );

                c1.IsRunning.Should().BeFalse();
                c2.IsRunning.Should().BeFalse();
                l1.IsRunning.Should().BeFalse();
                l2.IsRunning.Should().BeFalse();

                c1.IsDestroyed.Should().BeTrue();
                c2.IsDestroyed.Should().BeTrue();
                l1.IsDestroyed.Should().BeTrue();
                l2.IsDestroyed.Should().BeTrue();
            } );
        }

        static int DevicesChangedCount = 0;
        static Task DevicesChanged_Async( IActivityMonitor monitor, IDeviceHost e )
        {
            Interlocked.Increment( ref DevicesChangedCount );
            return Task.CompletedTask;
        }

        [Test]
        public async Task configuration_changes_are_detected_and_applied_by_the_DeviceConfigurator_hosted_service()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( configuration_changes_are_detected_and_applied_by_the_DeviceConfigurator_hosted_service ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C1:Status", "RunnableStarted" );

            await RunHost( config, async services =>
            {
                Debug.Assert( CameraHost.TestInstance != null );
                Debug.Assert( LightControllerHost.Instance != null );

                CameraHost.TestInstance.Should().BeSameAs( services.GetRequiredService<CameraHost>() );
                CameraHost.TestInstance.DevicesChanged.Async += DevicesChanged_Async; 

                Camera.TotalCount.Should().Be( 1 );
                Camera.TotalRunning.Should().Be( 1 );
                LightController.TotalCount.Should().Be( 0 );
                LightController.TotalRunning.Should().Be( 0 );
                DevicesChangedCount = 0;

                var c1 = CameraHost.TestInstance.Find( "C1" );
                Debug.Assert( c1 != null );
                c1.IsRunning.Should().BeTrue();

                config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C1:Status", "Disabled" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                c1.IsRunning.Should().BeFalse();
                DevicesChangedCount.Should().Be( 1 );

                config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C1:Status", "AlwaysRunning" );
                config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C2:Status", "Disabled" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                var c2 = CameraHost.TestInstance.Find( "C2" );
                Debug.Assert( c2 != null );

                c1.IsRunning.Should().BeTrue();
                c2.IsRunning.Should().BeFalse();

                // C1 configuration is "removed", but IsPartialConfiguration is true by default:
                // the device is not concerned by a missing configuration.
                config.Provider.Remove( "CK-DeviceModel:CameraHost:Items:C1" );
                config.Provider.Set( "CK-DeviceModel:CameraHost:Items:C2:Status", "AlwaysRunning" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                c1.IsRunning.Should().BeTrue();
                c2.IsRunning.Should().BeTrue();

                // Setting IsPartialConfiguration to false: the C1 device doesn't exist anymore!
                config.Provider.Set( "CK-DeviceModel:CameraHost:IsPartialConfiguration", "false" );
                config.Provider.RaiseChanged();
                await Task.Delay( 150 );

                c1.IsDestroyed.Should().BeTrue( "C1 is dead." );
                c2.IsRunning.Should().BeTrue();

                CameraHost.TestInstance.DevicesChanged.Async -= DevicesChanged_Async;
                await CameraHost.TestInstance.ClearAsync( TestHelper.Monitor );

                c1.IsDestroyed.Should().BeTrue();
                c2.IsDestroyed.Should().BeTrue();
            } );
        }


        async Task RunHost( DynamicConfiguration config, Func<IServiceProvider,Task> running, [CallerMemberName] string? caller = null )
        {
            using( TestHelper.Monitor.OpenInfo( $"Running host from '{caller}'." ) )
            {
                Camera.TotalCount.Should().Be( 0 );
                Camera.TotalRunning.Should().Be( 0 );
                LightController.TotalCount.Should().Be( 0 );
                LightController.TotalRunning.Should().Be( 0 );
                CameraHost.TestInstance.Should().BeNull();
                LightControllerHost.Instance.Should().BeNull();

                using( var host = new HostBuilder()
                                        .ConfigureAppConfiguration( builder => builder.AddConfiguration( config.Root ) )
                                        .ConfigureServices( services =>
                                        {
                                            services.AddSingleton( config.Root );
                                            // All this is done automatically by CKSetup.
                                            services.AddSingleton<LightControllerHost>();
                                            services.TryAddEnumerable( ServiceDescriptor.Singleton<IDeviceHost, LightControllerHost>( sp => sp.GetRequiredService<LightControllerHost>() ) );
                                            services.AddSingleton<CameraHost>();
                                            services.TryAddEnumerable( ServiceDescriptor.Singleton<IDeviceHost, CameraHost>( sp => sp.GetRequiredService<CameraHost>() ) );
                                            services.AddHostedService<DeviceHostDaemon>();
                                            services.AddSingleton<IDeviceAlwaysRunningPolicy, DefaultDeviceAlwaysRunningPolicy>();
                                            services.AddHostedService<DeviceConfigurator>();
                                        } )
                                        .Build() )
                {
                    await host.StartAsync();

                    // Checks that the TryAddEnumerable dos not instantiate its own singleton: this is why we use the (rather nasty)
                    // registration with the factory lambda above.
                    var allHosts = host.Services.GetServices<IDeviceHost>().ToList();
                    allHosts.Count.Should().Be( 2 );
                    var hosts = new IDeviceHost[] { host.Services.GetRequiredService<CameraHost>(), host.Services.GetRequiredService<LightControllerHost>() };
                    allHosts.Should().BeEquivalentTo( hosts, o => o.WithoutStrictOrdering() );

                    CameraHost.TestInstance.Should().NotBeNull();
                    LightControllerHost.Instance.Should().NotBeNull();

                    await running( host.Services );

                    await host.StopAsync();
                }

                CameraHost.TestInstance = null;
                LightControllerHost.Instance = null;
            }
        }
    }
}
