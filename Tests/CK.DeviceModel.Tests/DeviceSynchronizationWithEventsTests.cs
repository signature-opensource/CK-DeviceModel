using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// This test reproduces a sub set of the ObservableDeviceHost behavior.
    /// </summary>
    [TestFixture]
    public class DeviceSynchronizationWithEventsTests
    {
        class DeviceInfo
        {
            public DeviceInfo( IDevice d )
            {
                DeviceName = d.Name;
                IsRunning = d.IsRunning;
                ControllerKey = d.ControllerKey;
                ConfigurationControllerKey = d.ExternalConfiguration.ControllerKey;
            }

            public string DeviceName { get; }

            public bool IsRunning { get; set; }

            public string? ControllerKey { get; set; }

            public string? ConfigurationControllerKey { get; set; }

            internal void Update( IDevice device )
            {
                IsRunning = device.IsRunning;
            }
        }

        class Client
        {
            // The client lives in a non-concurrent world. This lock emulates this.
            readonly AsyncLock _lock;
            readonly IDeviceHost _host;
            readonly Dictionary<string, DeviceInfo> _devices;

            public Client( IDeviceHost host, string name )
            {
                Name = name;
                _lock = new AsyncLock( LockRecursionPolicy.NoRecursion, name );
                _host = host;
                _devices = new Dictionary<string, DeviceInfo>();
            }

            public async Task InitializeAsync( IActivityMonitor monitor )
            {
                // We start to listen to event in the asynchronous world.
                _host.AllDevicesLifetimeEvent.Async += OnDevicesLifetimeEventAsync;

                using( await _lock.LockAsync( monitor ) )
                {
                    // In real life, the dictionary may already contain device info (deserialization).
                    // We need to conciliate.
                    ReconciliateDevices( monitor, _host.GetDevices() );
                }
            }

            void ReconciliateDevices( IActivityMonitor monitor, IReadOnlyDictionary<string, IDevice> devices )
            {
                foreach( var d in devices.Values )
                {
                    if( _devices.TryGetValue( d.Name, out var deviceInfo ) )
                    {
                        deviceInfo.Update( d );
                    }
                    else _devices.Add( d.Name, new DeviceInfo( d ) );
                }
                List<string>? toRemove = null;
                foreach( var info in _devices.Values )
                {
                    if( !_devices.ContainsKey( info.DeviceName ) )
                    {
                        if( toRemove == null ) toRemove = new List<string>();
                        toRemove.Add( info.DeviceName );
                    }
                }
                if( toRemove != null )
                {
                    foreach( var n in toRemove )
                    {
                        _devices.Remove( n );
                    }
                }
            }

            async Task OnDevicesLifetimeEventAsync( IActivityMonitor monitor, IDeviceHost sender, DeviceLifetimeEvent e )
            {
                using( await _lock.LockAsync( monitor ) )
                {
                    if( e.Device.IsDestroyed )
                    {
                        monitor.Trace( $"TEST - Removing {e.Device.Name}." );
                        _devices.Remove( e.Device.Name );
                    }
                    else
                    {
                        if( _devices.TryGetValue( e.Device.Name, out var deviceInfo ) )
                        {
                            monitor.Trace( $"TEST - Updating {e.Device.Name}." );
                            deviceInfo.Update( e.Device );
                        }
                        else
                        {
                            monitor.Trace( $"TEST - Creating {e.Device.Name}." );
                            _devices.Add( e.Device.Name, new DeviceInfo( e.Device ) );
                        }
                    }
                }
            }

            public IReadOnlyDictionary<string, DeviceInfo> Devices => _devices;

            public string Name { get; }
        }

        class HostShaker
        {
            readonly IDeviceHost _host;
            readonly CancellationTokenSource _cancel;
            Task? _task;
            readonly int _maxDeviceCount;

            public HostShaker(IDeviceHost host, int maxDeviceCount )
            {
                _host = host;
                _maxDeviceCount = maxDeviceCount;
                _cancel = new CancellationTokenSource();
            }

            public Task StopAsync()
            {
                Debug.Assert( _task != null );
                _cancel.Cancel();
                return _task;
            }

            public void Start( int randomSeed )
            {
                Debug.Assert( _task == null );
                _task = StartAsync( randomSeed );
            }

            async Task StartAsync( int randomSeed )
            {
                var random = randomSeed == 0 ? new Random() : new Random( randomSeed );

                string RndName() => $"Device {random.Next( _maxDeviceCount )}";

                var monitor = new ActivityMonitor( nameof(HostShaker) );

                while( !_cancel.IsCancellationRequested )
                {
                    await Task.Delay( random.Next( 50 ) );
                    int action = random.Next( 7 );
                    if( action >= 4 )
                    {
                        var d = _host.Find( RndName() );
                        if( d != null )
                        {
                            if( action == 4 ) await d.DestroyAsync( monitor, waitForDeviceDestroyed: true );
                            else if( action == 5 ) await d.StartAsync( monitor );
                            else if( action == 6 ) await d.StopAsync( monitor );
                        }
                    }
                    else
                    {
                        await _host.EnsureDeviceAsync( monitor, new CommonScaleConfiguration()
                        {
                            Name = RndName(),
                            PhysicalRate = 20 + random.Next( 100 ),
                            Status = (DeviceConfigurationStatus)action
                        } );
                    }
                }

                monitor.MonitorEnd();
            }
        }

        [TestCase( 3712, 2000, "SimpleActive" )]
        [TestCase( 3713, 2000, "SimpleActive" )]
        [TestCase( 3714, 2000, "SimpleActive" )]
        [TestCase( 0, 3000, "SimpleActive" )]
        [TestCase( 3712, 2000, "Active" )]
        [TestCase( 3713, 2000, "Active" )]
        [TestCase( 3714, 2000, "Active" )]
        [TestCase( 0, 3000, "Active" )]
        public async Task device_list_sync_Async( int seed, int runtimeMS, string deviceType )
        {
            if( seed == 0 ) seed = Random.Shared.Next();
            using var _ = TestHelper.Monitor.OpenInfo( $"{nameof( device_list_sync_Async )}-{seed}-{runtimeMS}" );

            //TestHelper.Monitor.AutoTags += ActivityMonitor.Tags.StackTrace;
            //Action<IActivityMonitor> setStackTrace = m => m.AutoTags += ActivityMonitor.Tags.StackTrace;
            //ActivityMonitor.AutoConfiguration += setStackTrace;
            //using var cleanupStackTrace = Util.CreateDisposableAction( () =>
            //{
            //    TestHelper.Monitor.AutoTags -= ActivityMonitor.Tags.StackTrace;
            //    ActivityMonitor.AutoConfiguration -= setStackTrace;
            //} );

            var random = new Random( seed );
            var monitor = TestHelper.Monitor;

            IDeviceHost host = deviceType == "SimpleActive" ? new SimpleScaleHost() : new ScaleHost();

            var shaker = new HostShaker( host, 10 );
            bool startShakeBeforeClients = random.Next( 2 ) == 0;
            if( startShakeBeforeClients ) shaker.Start( seed );
            
            var clients = new Client[1 + random.Next(3)];
            for( int i = 0; i < clients.Length; ++i )
            {
                clients[i] = new Client( host, $"Client{i}" );
            }
            // Clients are not synchronized between them.
            await Task.WhenAll( clients.Select( c => c.InitializeAsync( monitor ) ).ToArray() );

            if( !startShakeBeforeClients ) shaker.Start( seed );

            await Task.Delay( runtimeMS );

            await shaker.StopAsync();


            var devices = host.GetDevices().Values
                                .OrderBy( d => d.Name )
                                .Select( d => $"{d.Name}[{d.IsRunning},{d.ControllerKey},{d.ExternalConfiguration.ControllerKey}]" )
                                .Concatenate();
            bool success = true;
            for( int i = 0; i < clients.Length; ++i )
            {
                var infos = clients[i].Devices.Values.OrderBy( i => i.DeviceName )
                                        .Select( i => $"{i.DeviceName}[{i.IsRunning},{i.ControllerKey},{i.ConfigurationControllerKey}]" )
                                        .Concatenate();
                if( infos != devices )
                {
                    using( monitor.OpenError( $"Client: {clients[i].Name} error." ) )
                    {
                        monitor.Error( $"Infos:   {infos}" );
                        monitor.Error( $"Devices: {devices}" );
                    }
                    success = false;
                }
            }
            await host.ClearAsync( monitor, true );
            success.Should().BeTrue();
        }
    }
}
