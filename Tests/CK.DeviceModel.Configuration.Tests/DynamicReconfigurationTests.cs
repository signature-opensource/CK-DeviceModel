using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace CK.DeviceModel.Configuration.Tests
{
    [TestFixture]
    public class DynamicReconfigurationTests
    {

        [TypeConverter(typeof(Converter))]
        public readonly struct FakeNormalizedPath 
        {
            sealed class Converter : TypeConverter
            {
                public override bool CanConvertFrom( ITypeDescriptorContext? context, Type sourceType )
                {
                    return sourceType == typeof( string );
                }

                public override bool CanConvertTo( ITypeDescriptorContext? context, Type? destinationType )
                {
                    return destinationType == typeof( string );
                }

                public override object? ConvertFrom( ITypeDescriptorContext? context, CultureInfo? culture, object value )
                {
                    return value is string s ? new FakeNormalizedPath( s ) : default;
                }

                public override object? ConvertTo( ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType )
                {
                    return value is FakeNormalizedPath p ? p.ToString() : string.Empty;
                }
            }

            readonly string _v;

            public FakeNormalizedPath( string p )  => _v = p;

            public static implicit operator string( FakeNormalizedPath p ) => p._v ?? string.Empty;

            public static implicit operator FakeNormalizedPath( string p ) => new FakeNormalizedPath( p );

            public override string ToString() => _v ?? string.Empty;
        }

        public class CameraDeviceConfiguration : DeviceConfiguration
        {
            public CameraDeviceConfiguration()
            {
            }

            public FakeNormalizedPath Topic { get; set; }

            public int Power { get; set; }

            public CameraDeviceConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte(); // version
                Topic = r.ReadString();
                Power = r.ReadInt32();
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.Write( Topic );
                w.Write( Power );
            }
        }

        public class CameraDevice : Device<CameraDeviceConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public CameraDevice( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Interlocked.Increment( ref TotalCount );
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraDeviceConfiguration config )
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

        public class CameraDeviceHost : DeviceHost<CameraDevice, DeviceHostConfiguration<CameraDeviceConfiguration>, CameraDeviceConfiguration>
        {
            public static CameraDeviceHost? TestInstance;

            public CameraDeviceHost()
            {
                TestInstance = this;
            }
        }

        public class LightControllerDeviceConfiguration : DeviceConfiguration
        {
            public LightControllerDeviceConfiguration()
            {
            }

            public LightControllerDeviceConfiguration( ICKBinaryReader r )
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

        public class LightControllerDevice : Device<LightControllerDeviceConfiguration>
        {
            public static int TotalCount;
            public static int TotalRunning;

            public LightControllerDevice( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Interlocked.Increment( ref TotalCount );
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, LightControllerDeviceConfiguration config )
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

        public class LightControllerDeviceHost : DeviceHost<LightControllerDevice, DeviceHostConfiguration<LightControllerDeviceConfiguration>, LightControllerDeviceConfiguration>
        {
            public static LightControllerDeviceHost? Instance;

            public LightControllerDeviceHost()
            {
                Instance = this;
            }
        }

        class InvalidDeviceConfig : DeviceConfiguration
        {
            protected override bool DoCheckValid( IActivityMonitor monitor ) => false;
        }

        class InvalidDevice : Device<InvalidDeviceConfig>
        {
            public InvalidDevice( IActivityMonitor monitor, CreateInfo info ) : base( monitor, info )
            {
            }

            protected override Task DoDestroyAsync( IActivityMonitor monitor ) => throw new NotImplementedException();

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, InvalidDeviceConfig config ) => throw new NotImplementedException();

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason ) => throw new NotImplementedException();

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason ) => throw new NotImplementedException();
        }


        [Test]
        public void a_new_DeviceConfiguration_MUST_be_valid()
        {
            FluentActions.Invoking( TouchInvalidConfiguration ).Should().Throw<TypeInitializationException>(); 
        }

        [MethodImpl( MethodImplOptions.NoInlining )]
        static void TouchInvalidConfiguration()
        {
            InvalidDevice invalid = new InvalidDevice( default!, default );
            TestHelper.Monitor.Info( $"The invalid is {invalid}." );
        }

        [Test]
        public async Task empty_configuration_does_not_create_any_device_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( empty_configuration_does_not_create_any_device_Async ) );

            var config = DynamicConfiguration.Create();
            await RunHostAsync( config, services =>
            {
                CameraDevice.TotalCount.Should().Be( 0 );
                CameraDevice.TotalRunning.Should().Be( 0 );
                LightControllerDevice.TotalCount.Should().Be( 0 );
                LightControllerDevice.TotalRunning.Should().Be( 0 );
                return Task.CompletedTask;
            } );
        }

        [Test]
        public async Task initial_configuration_create_devices_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( initial_configuration_create_devices_Async ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C2:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:LightControllerDeviceHost:Items:L1:Status", "Disabled" );
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Topic", "IAm.Camera1" );
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Power", "3712" );
            await RunHostAsync( config, services =>
            {
                Debug.Assert( CameraDeviceHost.TestInstance != null );
                Debug.Assert( LightControllerDeviceHost.Instance != null );

                CameraDevice.TotalCount.Should().Be( 2 );
                CameraDevice.TotalRunning.Should().Be( 0 );
                LightControllerDevice.TotalCount.Should().Be( 1 );
                LightControllerDevice.TotalRunning.Should().Be( 0 );
                var c1 = CameraDeviceHost.TestInstance.Find( "C1" );
                var c2 = CameraDeviceHost.TestInstance.Find( "C2" );
                var l1 = LightControllerDeviceHost.Instance.Find( "L1" );
                Debug.Assert( c1 != null && c2 != null && l1 != null );

                c1.ExternalConfiguration.Power.Should().Be( 3712 );
                c1.ExternalConfiguration.Topic.ToString().Should().Be( "IAm.Camera1" );

                c1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable );
                c2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable );
                l1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
                return Task.CompletedTask;
            } );
        }

        [Test]
        public async Task initial_configuration_can_start_devices_and_then_they_live_their_lifes_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( initial_configuration_can_start_devices_and_then_they_live_their_lifes_Async ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Status", "RunnableStarted" );
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C2:Status", "AlwaysRunning" );
            config.Provider.Set( "CK-DeviceModel:LightControllerDeviceHost:Items:L1:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:LightControllerDeviceHost:Items:L2:Status", "Disabled" );
            await RunHostAsync( config, async services =>
            {
                Debug.Assert( CameraDeviceHost.TestInstance != null );
                Debug.Assert( LightControllerDeviceHost.Instance != null );

                CameraDevice.TotalCount.Should().Be( 2 );
                CameraDevice.TotalRunning.Should().Be( 2 );
                LightControllerDevice.TotalCount.Should().Be( 2 );
                LightControllerDevice.TotalRunning.Should().Be( 0 );
                var c1 = CameraDeviceHost.TestInstance.Find( "C1" );
                var c2 = CameraDeviceHost.TestInstance.Find( "C2" );
                var l1 = LightControllerDeviceHost.Instance.Find( "L1" );
                var l2 = LightControllerDeviceHost.Instance.Find( "L2" );
                Debug.Assert( c1 != null && c2 != null && l1 != null && l2 != null );

                c1.IsRunning.Should().BeTrue();
                c1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.RunnableStarted );

                c2.IsRunning.Should().BeTrue();
                c2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );

                l1.IsRunning.Should().BeFalse();
                l1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable );

                l2.IsRunning.Should().BeFalse();
                l2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );

                (await c1.StopAsync( TestHelper.Monitor )).Should().BeTrue();
                c1.IsRunning.Should().BeFalse();
                c1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.RunnableStarted );
                (await c1.StopAsync( TestHelper.Monitor )).Should().BeTrue( "One can always stop an already stopped device." );

                (await c2.StopAsync( TestHelper.Monitor )).Should().BeFalse( "c2 is AlwaysRunning." );
                c2.IsRunning.Should().BeTrue();
                c2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );

                (await l1.StartAsync( TestHelper.Monitor )).Should().BeTrue();
                l1.IsRunning.Should().BeTrue();
                l1.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable );

                (await l2.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled!" );
                l2.IsRunning.Should().BeFalse();
                l2.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );

                c1.IsDestroyed.Should().BeFalse();
                c2.IsDestroyed.Should().BeFalse();
                l1.IsDestroyed.Should().BeFalse();
                l2.IsDestroyed.Should().BeFalse();

                await CameraDeviceHost.TestInstance.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
                await LightControllerDeviceHost.Instance.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );

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

        class ChangeCounter : IDisposable
        {
            readonly IDeviceHost _host;

            public int DevicesChangedCount { get; private set; }
            public int DeviceConfigurationChangedCount => _devices.Values.Sum();

            readonly Dictionary<IDevice, int> _devices;

            public ChangeCounter( IDeviceHost host )
            {
                _host = host;
                _devices = new Dictionary<IDevice, int>();
                RegisteDeviceLifetimeEvents();
                host.DevicesChanged.Sync += ( m, h, devices ) =>
                {
                    ++DevicesChangedCount;
                    RegisteDeviceLifetimeEvents();
                };
            }

            private void RegisteDeviceLifetimeEvents()
            {
                foreach( var d in _host.GetDevices().Values )
                {
                    if( _devices.TryAdd( d, 0 ) )
                    {
                        d.LifetimeEvent.Sync += OnDeviceEvent;
                    }
                }
            }

            void OnDeviceEvent( IActivityMonitor m, DeviceLifetimeEvent e )
            {
                _devices[e.Device] = _devices[e.Device] + 1;
            }

            public void Dispose()
            {
                foreach( var d in _devices.Keys )
                {
                    d.LifetimeEvent.Sync -= OnDeviceEvent;
                }
            }
        }

        [Test]
        public async Task configuration_changes_are_detected_and_applied_by_the_DeviceConfigurator_hosted_service_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( configuration_changes_are_detected_and_applied_by_the_DeviceConfigurator_hosted_service_Async ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Status", "RunnableStarted" );

            await RunHostAsync( config, async services =>
            {
                Debug.Assert( CameraDeviceHost.TestInstance != null );
                Debug.Assert( LightControllerDeviceHost.Instance != null );

                CameraDeviceHost.TestInstance.Should().BeSameAs( services.GetRequiredService<CameraDeviceHost>() );
                using var counter = new ChangeCounter( CameraDeviceHost.TestInstance );

                CameraDevice.TotalCount.Should().Be( 1 );
                CameraDevice.TotalRunning.Should().Be( 1 );
                LightControllerDevice.TotalCount.Should().Be( 0 );
                LightControllerDevice.TotalRunning.Should().Be( 0 );
                counter.DevicesChangedCount.Should().Be( 0 );
                counter.DeviceConfigurationChangedCount.Should().Be( 0 );

                var c1 = CameraDeviceHost.TestInstance.Find( "C1" );
                Debug.Assert( c1 != null );
                c1.IsRunning.Should().BeTrue();

                config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Status", "Disabled" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                c1.IsRunning.Should().BeFalse();
                counter.DevicesChangedCount.Should().Be( 0 );
                counter.DeviceConfigurationChangedCount.Should().Be( 1 );

                config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C1:Status", "AlwaysRunning" );
                config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C2:Status", "Disabled" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                var c2 = CameraDeviceHost.TestInstance.Find( "C2" );
                Debug.Assert( c2 != null );

                c1.IsRunning.Should().BeTrue();
                c2.IsRunning.Should().BeFalse();

                // C1 configuration is "removed", but IsPartialConfiguration is true by default:
                // the device is not concerned by a missing configuration.
                config.Provider.Remove( "CK-DeviceModel:CameraDeviceHost:Items:C1" );
                config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:Items:C2:Status", "AlwaysRunning" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                c1.IsRunning.Should().BeTrue();
                c2.IsRunning.Should().BeTrue();

                // Setting IsPartialConfiguration to false: the C1 device doesn't exist anymore!
                config.Provider.Set( "CK-DeviceModel:CameraDeviceHost:IsPartialConfiguration", "false" );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                c1.IsDestroyed.Should().BeTrue( "C1 is dead." );
                c2.IsRunning.Should().BeTrue();

                await c2.DestroyAsync( TestHelper.Monitor );
            } );
        }

        [Test]
        public async Task DeviceHostDaemon_is_configured_by_optional_Daemon_section_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( DeviceHostDaemon_is_configured_by_optional_Daemon_section_Async ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:Daemon:StoppedBehavior", "not a valid name." );

            await RunHostAsync( config, async services =>
            {
                var daemon = services.GetRequiredService<DeviceHostDaemon>();

                daemon.StoppedBehavior.Should().Be( OnStoppedDaemonBehavior.None );

                config.Provider.Set( "CK-DeviceModel:Daemon:StoppedBehavior", OnStoppedDaemonBehavior.ClearAllHosts.ToString() );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                daemon.StoppedBehavior.Should().Be( OnStoppedDaemonBehavior.ClearAllHosts );

                config.Provider.Set( "CK-DeviceModel:Daemon:StoppedBehavior", OnStoppedDaemonBehavior.ClearAllHostsAndWaitForDevicesDestroyed.ToString() );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                daemon.StoppedBehavior.Should().Be( OnStoppedDaemonBehavior.ClearAllHostsAndWaitForDevicesDestroyed );

                config.Provider.Set( "CK-DeviceModel:Daemon:StoppedBehavior", OnStoppedDaemonBehavior.None.ToString() );
                config.Provider.RaiseChanged();
                await Task.Delay( 50 );

                daemon.StoppedBehavior.Should().Be( OnStoppedDaemonBehavior.None );

            } );
        }


        async Task RunHostAsync( DynamicConfiguration config, Func<IServiceProvider,Task> running, [CallerMemberName] string? caller = null )
        {
            using( TestHelper.Monitor.OpenInfo( $"Running host from '{caller}'." ) )
            {
                CameraDevice.TotalCount.Should().Be( 0 );
                CameraDevice.TotalRunning.Should().Be( 0 );
                LightControllerDevice.TotalCount.Should().Be( 0 );
                LightControllerDevice.TotalRunning.Should().Be( 0 );
                CameraDeviceHost.TestInstance.Should().BeNull();
                LightControllerDeviceHost.Instance.Should().BeNull();

                using( var host = new HostBuilder()
                                        .ConfigureAppConfiguration( builder => builder.AddConfiguration( config.Root ) )
                                        .ConfigureServices( services =>
                                        {
                                            services.AddSingleton( config.Root );
                                            // All this is done automatically by CKSetup.
                                            services.AddSingleton<LightControllerDeviceHost>();
                                            services.TryAddEnumerable( ServiceDescriptor.Singleton<IDeviceHost, LightControllerDeviceHost>( sp => sp.GetRequiredService<LightControllerDeviceHost>() ) );
                                            services.AddSingleton<CameraDeviceHost>();
                                            services.TryAddEnumerable( ServiceDescriptor.Singleton<IDeviceHost, CameraDeviceHost>( sp => sp.GetRequiredService<CameraDeviceHost>() ) );
                                            services.AddHostedService<DeviceHostDaemon>();
                                            services.AddSingleton<IDeviceAlwaysRunningPolicy, DefaultDeviceAlwaysRunningPolicy>();
                                            services.AddSingleton<DeviceHostDaemon>();
                                            services.AddHostedService<DeviceConfigurator>();
                                        } )
                                        .Build() )
                {
                    await host.StartAsync();

                    // Checks that the TryAddEnumerable dos not instantiate its own singleton: this is why we use the (rather nasty)
                    // registration with the factory lambda above.
                    var allHosts = host.Services.GetServices<IDeviceHost>().ToList();
                    allHosts.Count.Should().Be( 2 );
                    var hosts = new IDeviceHost[] { host.Services.GetRequiredService<CameraDeviceHost>(), host.Services.GetRequiredService<LightControllerDeviceHost>() };
                    allHosts.Should().BeEquivalentTo( hosts, o => o.WithoutStrictOrdering() );

                    CameraDeviceHost.TestInstance.Should().NotBeNull();
                    LightControllerDeviceHost.Instance.Should().NotBeNull();

                    await running( host.Services );

                    await host.StopAsync();
                }

                CameraDeviceHost.TestInstance = null;
                LightControllerDeviceHost.Instance = null;
            }
        }
    }
}
