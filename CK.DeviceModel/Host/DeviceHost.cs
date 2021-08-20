using CK.Core;
using CK.PerfectEvent;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for <see cref="IDeviceHost"/> implementation.
    /// <see cref="IDeviceHost"/> is a <see cref="ISingletonAutoService"/> that has the <see cref="IsMultipleAttribute"/>.
    /// This is not a <see cref="Microsoft.Extensions.Hosting.IHostedService"/>: a device host has no Start/Stop, it is
    /// passive and only manages its set of devices.
    /// </summary>
    /// <typeparam name="T">The Device type.</typeparam>
    /// <typeparam name="THostConfiguration">The configuration type for this host.</typeparam>
    /// <typeparam name="TConfiguration">The Device's configuration type.</typeparam>
    [CKTypeDefiner]
    public abstract partial class DeviceHost<T, THostConfiguration, TConfiguration> : IDeviceHost, IInternalDeviceHost
        where T : Device<TConfiguration>
        where THostConfiguration : DeviceHostConfiguration<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// This is the whole state of this Host. It is updated atomically (by setting a
        /// new dictionary instance). All Find methods (and RouteCommand) can use it lock-free.
        /// </summary>
        Dictionary<string, ConfiguredDevice<T, TConfiguration>> _devices;

        readonly PerfectEventSender<IDeviceHost> _devicesChanged;

        /// <summary>
        /// This lock uses the NoRecursion policy.
        /// It protects the whole ApplyConfigurationAsync: only one global reconfiguration is
        /// allowed at a time.
        /// Reconfigurations or destruction can concurrently happen when the IDevice methods are used.
        /// </summary>
        readonly AsyncLock _applyConfigAsynclock;

        // Compile cached lambda.
        readonly Func<THostConfiguration> _hostConfigFactory;

        /// <summary>
        /// ApplyConfigurationAsync starts by creating this by copying the lock free _devices inside
        /// the _reconfigureSyncLock.
        /// <para>
        /// Then it first handles the creation of the new devices (still in the _reconfigureSyncLock)
        /// and creates 3 lists: one with the device to reconfigure (the ones that already exist), one with the new devices
        /// to start (AlwaysRunning or RunnableStarted) and one with the devices to destroy (if the configuration is not partial).
        /// The _reconfigureSyncLock is released and the device InternalReconfigureAsync, StartAsync or DestroyAsync (from the 3
        /// lists) are called, just as if they were called from any other threads.
        /// </para>
        /// <para>
        /// These device's methods call the synchronous OnDeviceReconfigured or OnDeviceDestroyed. They enter
        /// the _reconfigureSyncLock and update the _reconfiguringDevices if it's not null or
        /// create a new dictionary (copying the _devices), update it and set it as the new _devices.
        /// </para>
        /// <para>
        /// Once ApplyConfigurationAsync is done calling its ReconfigureAsync or DestroyAsync methods,
        /// it enters the _reconfigureSyncLock for the last time to set the _devices be the _reconfiguringDevices
        /// and to reset the _reconfiguringDevices to null. It finally returns the array of DeviceConfigurationResult.
        /// </para>
        /// </summary>
        Dictionary<string, ConfiguredDevice<T, TConfiguration>>? _reconfiguringDevices;
        bool _reconfiguringDevicesChanged;
        readonly object _reconfigureSyncLock;

        /// <summary>
        /// Initializes a new host.
        /// </summary>
        /// <param name="deviceHostName">A name that SHOULD identify this host instance unambiguously in a running context.</param>
        protected DeviceHost( string deviceHostName )
            : this( true )
        {
            if( String.IsNullOrWhiteSpace( deviceHostName ) ) throw new ArgumentException( nameof( deviceHostName ) );
            // The name of the lock is the DeviceHostName.
            _applyConfigAsynclock = new AsyncLock( LockRecursionPolicy.NoRecursion, deviceHostName );
        }

        /// <summary>
        /// Initializes a new host with a <see cref="DeviceHostName"/> sets to its type name.
        /// </summary>
        protected DeviceHost()
            : this( true )
        {
            _applyConfigAsynclock = new AsyncLock( LockRecursionPolicy.NoRecursion, GetType().Name );
        }

        // This is the only CS8618 warning that must be raised here: Non-nullable field '_applyConfigAsynclock' is uninitialized.
        DeviceHost( bool privateCall )
        {
            _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
            _devicesChanged = new PerfectEventSender<IDeviceHost>();

            var t = typeof( THostConfiguration );
            var ctor = t.GetConstructor( Type.EmptyTypes );
            if( ctor == null ) throw new InvalidOperationException( $"Type '{t.Name}' must have a default public constructor." );
            var m = new DynamicMethod( "CreateInstance", t, Type.EmptyTypes, true );
            ILGenerator ilGenerator = m.GetILGenerator();
            ilGenerator.Emit( OpCodes.Newobj, ctor );
            ilGenerator.Emit( OpCodes.Ret );
            _hostConfigFactory = (Func<THostConfiguration>)m.CreateDelegate( typeof( Func<THostConfiguration> ) );
            _reconfigureSyncLock = new object();
            _alwayRunningStopped = new List<(IDevice Device, int Count, DateTime NextCall)>();
            _alwayRunningStoppedSafe = Array.Empty<(IDevice, int, DateTime)>();
        }

        /// <inheritdoc />
        public string DeviceHostName => _applyConfigAsynclock.Name;

        /// <inheritdoc />
        public int Count => _devices.Count;

        Type IDeviceHost.GetDeviceHostConfigurationType() => typeof( THostConfiguration );

        Type IDeviceHost.GetDeviceConfigurationType() => typeof( TConfiguration );

        DeviceCommandWithResult<DeviceApplyConfigurationResult> IInternalDeviceHost.CreateReconfigureCommand( string name ) => new InternalReconfigureDeviceCommand<TConfiguration>( GetType(), name );

        BaseStartDeviceCommand IInternalDeviceHost.CreateStartCommand( string name ) => new InternalStartDeviceCommand( GetType(), name );

        BaseStopDeviceCommand IInternalDeviceHost.CreateStopCommand( string name, bool ignoreAlwaysRunning ) => new InternalStopDeviceCommand( GetType(), name, ignoreAlwaysRunning );

        BaseDestroyDeviceCommand IInternalDeviceHost.CreateDestroyCommand( string name ) => new InternalDestroyDeviceCommand( GetType(), name );

        BaseSetControllerKeyDeviceCommand IInternalDeviceHost.CreateSetControllerKeyDeviceCommand( string name, string? current, string? newControllerKey ) => new InternalSetControllerKeyDeviceCommand( GetType(), name, current, newControllerKey );

        bool IInternalDeviceHost.OnDeviceConfigured( IActivityMonitor monitor, IDevice device, DeviceApplyConfigurationResult result, DeviceConfiguration externalConfig )
        {
            var configured = new ConfiguredDevice<T, TConfiguration>( (T)device, (TConfiguration)externalConfig, result );
            lock( _reconfigureSyncLock )
            {
                if( _reconfiguringDevices != null )
                {
                    _reconfiguringDevices[device.Name] = configured;
                    _reconfiguringDevicesChanged = true;
                    return false;
                }
                var devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices )
                {
                    [device.Name] = configured
                };
                _devices = devices;
                return true;
            }
        }

        bool IInternalDeviceHost.OnDeviceDestroyed( IActivityMonitor monitor, IDevice device )
        {
            lock( _reconfigureSyncLock )
            {
                if( _reconfiguringDevices != null )
                {
                    _reconfiguringDevices.Remove( device.Name );
                    _reconfiguringDevicesChanged = true;
                    return false;
                }
                var devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                devices.Remove( device.Name );
                _devices = devices;
                return true;
            }
        }

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        public T? this[string deviceName] => Find( deviceName );

        /// <summary>
        /// Gets a device by its name (explicit the <see cref="this[string]"/> indexer).
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        public T? Find( string deviceName ) => _devices.GetValueOrDefault( deviceName ).Device;

        IDevice? IDeviceHost.Find( string deviceName ) => Find( deviceName );

        /// <summary>
        /// Gets a device and its applied configuration by its name.
        /// See <see cref="ConfiguredDevice{T, TConfiguration}.Configuration"/>.
        /// <para>
        /// Use <see cref="Find(string)"/> to easily get the <see cref="ConfiguredDevice{T, TConfiguration}.Device"/> if it exists.
        /// </para>
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device and its configuration or null if not found.</returns>
        public ConfiguredDevice<T, TConfiguration>? GetConfiguredDevice( string deviceName )
        {
            return _devices.TryGetValue( deviceName, out var e ) ? e : null;
        }

        (IDevice?, DeviceConfiguration?) IDeviceHost.GetConfiguredDevice( string deviceName )
        {
            return _devices.TryGetValue( deviceName, out var e ) ? (e.Device, e.Configuration) : (null, null);
        }

        /// <summary>
        /// Gets a snapshot of the current devices and their configurations that satisfy a predicate.
        /// Note that these objects are a copy of the ones that are used by the actual devices.
        /// See <see cref="ConfiguredDevice{T, TConfiguration}.Configuration"/>.
        /// </summary>
        /// <param name="predicate">Optional predicate to filter the snapshotted result.</param>
        /// <returns>The snapshot of the configured devices.</returns>
        public IReadOnlyList<ConfiguredDevice<T, TConfiguration>> GetConfiguredDevices( Func<T, TConfiguration, bool>? predicate = null )
        {
            return predicate != null
                        ? _devices.Values.Where( e => predicate( e.Device, e.Configuration ) ).ToArray()
                        : _devices.Values.ToArray();
        }

        IReadOnlyList<(IDevice, DeviceConfiguration)> IDeviceHost.GetConfiguredDevices( Func<IDevice, DeviceConfiguration, bool>? predicate )
        {
            var set = predicate != null ? _devices.Values.Where( e => predicate( e.Device, e.Configuration ) ) : _devices.Values;
            return set.Select( e => ((IDevice)e.Device, (DeviceConfiguration)e.Configuration) ).ToArray();
        }

        /// <inheritdoc />
        public PerfectEvent<IDeviceHost> DevicesChanged => _devicesChanged.PerfectEvent;

        /// <summary>
        /// Captures the result of <see cref="ApplyConfigurationAsync"/>.
        /// </summary>
        public readonly struct ConfigurationResult
        {
            readonly IReadOnlyCollection<string>? _destroyedNames;

            /// <summary>
            /// Error constructor: only the initial configuration is provided.
            /// </summary>
            /// <param name="initialConfiguration">The configuration.</param>
            internal ConfigurationResult( THostConfiguration initialConfiguration )
            {
                Success = false;
                HostConfiguration = initialConfiguration;
                var r = new DeviceApplyConfigurationResult[initialConfiguration.Items.Count];
                Array.Fill( r, DeviceApplyConfigurationResult.InvalidConfiguration );
                Results = r;
                _destroyedNames = null;
            }

            internal ConfigurationResult( bool success, THostConfiguration initialConfiguration, DeviceApplyConfigurationResult[] r, HashSet<string>? destroyedNames )
            {
                Success = success;
                HostConfiguration = initialConfiguration;
                Results = r;
                _destroyedNames = destroyedNames;
            }

            /// <summary>
            /// Gets whether the configuration of the host succeeded.
            /// </summary>           
            public bool Success { get; }

            /// <summary>
            /// Gets whether the error is due to an invalid or rejected <see cref="HostConfiguration"/>
            /// (detailed <see cref="Results"/> for each device is null in such case).
            /// </summary>
            public bool InvalidHostConfiguration => !Success && _destroyedNames == null;

            /// <summary>
            /// Gets the original configuration.
            /// </summary>
            public THostConfiguration HostConfiguration { get; }

            /// <summary>
            /// Gets the detailed results for each <see cref="IDeviceHostConfiguration.Items"/>.
            /// If <see cref="InvalidHostConfiguration"/> is true, they are all <see cref="DeviceApplyConfigurationResult.InvalidConfiguration"/>.
            /// </summary>
            public IReadOnlyList<DeviceApplyConfigurationResult> Results { get; }

            /// <summary>
            /// Gets the device names that have been destroyed if <see cref="IDeviceHostConfiguration.IsPartialConfiguration"/> is false.
            /// Empty otherwise.
            /// </summary>
            public IReadOnlyCollection<string> DestroyedDeviceNames => _destroyedNames ?? Array.Empty<string>();
        }

        Task<DeviceApplyConfigurationResult> IDeviceHost.EnsureDeviceAsync( IActivityMonitor monitor, DeviceConfiguration configuration )
        {
            return EnsureDeviceAsync( monitor, (TConfiguration)configuration );
        }

        /// <summary>
        /// Applies a device configuration: this ensures that the device exists (it is created if needed) and is
        /// configured by the provided <paramref name="configuration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <returns>the result of the device configuration.</returns>
        public async Task<DeviceApplyConfigurationResult> EnsureDeviceAsync( IActivityMonitor monitor, TConfiguration configuration )
        {
            if( configuration == null ) throw new ArgumentNullException( nameof( configuration ) );

            THostConfiguration hostConfig = _hostConfigFactory();
            hostConfig.Items.Add( configuration );
            var result = await ApplyConfigurationAsync( monitor, hostConfig ).ConfigureAwait( false );
            return result.Results[0];
        }

        async Task<bool> IDeviceHost.ApplyConfigurationAsync( IActivityMonitor monitor, IDeviceHostConfiguration configuration, bool allowEmptyConfiguration )
        {
            var r = await ApplyConfigurationAsync( monitor, (THostConfiguration)configuration, allowEmptyConfiguration ).ConfigureAwait( false );
            return r.Success;
        }

        /// <summary>
        /// Applies a host configuration: multiple devices can be configured at once and if <see cref="DeviceHostConfiguration{TConfiguration}.IsPartialConfiguration"/> is false,
        /// devices for which no configuration appear are stopped and destroyed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <param name="allowEmptyConfiguration">By default, an empty configuration is considered as an error.</param>
        /// <returns>The composite result of the potentially multiple configurations.</returns>
        public async Task<ConfigurationResult> ApplyConfigurationAsync( IActivityMonitor monitor, THostConfiguration configuration, bool allowEmptyConfiguration = false )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( configuration == null ) throw new ArgumentNullException( nameof( configuration ) );

            var safeConfig = configuration.DeepClone();
            if( !safeConfig.CheckValidity( monitor, allowEmptyConfiguration ) ) return new ConfigurationResult( configuration );

            DeviceApplyConfigurationResult[] results = new DeviceApplyConfigurationResult[safeConfig.Items.Count];

            bool success = true;
            using( monitor.OpenInfo( $"Reconfiguring '{DeviceHostName}'. Applying {safeConfig.Items.Count} device configurations ({(safeConfig.IsPartialConfiguration ? "partial" : "full")} configuration)." ) )
            {
                await _applyConfigAsynclock.EnterAsync( monitor ).ConfigureAwait( false );
                try
                {
                    HashSet<string>? toDestroy = null;
                    List<(int, IDevice)>? toStart = null;
                    List<(int, T)>? toReconfigure = null;

                    // Capturing what we need to be able to work outside the sync lock.
                    lock( _reconfigureSyncLock )
                    {
                        _reconfiguringDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                        _reconfiguringDevicesChanged = false;
                        if( !safeConfig.IsPartialConfiguration ) toDestroy = new HashSet<string>( _reconfiguringDevices.Keys );
                        int configIdx = 0;
                        foreach( var c in safeConfig.Items )
                        {
                            if( !_reconfiguringDevices.TryGetValue( c.Name, out var configured ) )
                            {
                                using( monitor.OpenTrace( $"Creating new device '{c.Name}'." ) )
                                {
                                    var d = SafeCreateDevice( monitor, c );
                                    Debug.Assert( d == null || d.Name == c.Name );
                                    if( d == null )
                                    {
                                        results[configIdx] = DeviceApplyConfigurationResult.CreateFailed;
                                        success = false;
                                    }
                                    else
                                    {
                                        if( c.Status == DeviceConfigurationStatus.RunnableStarted || c.Status == DeviceConfigurationStatus.AlwaysRunning )
                                        {
                                            if( toStart == null ) toStart = new List<(int, IDevice)>();
                                            toStart.Add( (configIdx, d) );
                                        }
                                        else
                                        {
                                            _reconfiguringDevices.Add( c.Name, new ConfiguredDevice<T, TConfiguration>( d, c, DeviceApplyConfigurationResult.CreateSucceeded ) );
                                            results[configIdx] = DeviceApplyConfigurationResult.CreateSucceeded;
                                            _reconfiguringDevicesChanged = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if( toDestroy != null ) toDestroy.Remove( c.Name );
                                if( toReconfigure == null ) toReconfigure = new List<(int, T)>();
                                toReconfigure.Add( (configIdx, configured.Device) );
                            }
                            ++configIdx;
                        }
                    }

                    if( toDestroy != null && toDestroy.Count > 0 )
                    {
                        using( monitor.OpenInfo( $"Destroying {toDestroy.Count} devices." ) )
                        {
                            foreach( var n in toDestroy )
                            {
                                var d = Find( n );
                                if( d != null && !d.IsDestroyed )
                                {
                                    await d.DestroyAsync( monitor ).ConfigureAwait( false );
                                }
                                else
                                {
                                    monitor.Info( $"Device '{n}' is already destroyed." );
                                }
                            }
                        }
                    }
                    if( toReconfigure != null )
                    {
                        using( monitor.OpenInfo( $"Reconfiguring {toReconfigure.Count} devices." ) )
                        {
                            foreach( var (idx,d) in toReconfigure )
                            {
                                DeviceApplyConfigurationResult r = await d.InternalReconfigureAsync( monitor, configuration.Items[idx], safeConfig.Items[idx] ).ConfigureAwait( false );
                                results[idx] = r;
                                success &= (r == DeviceApplyConfigurationResult.UpdateSucceeded || r == DeviceApplyConfigurationResult.None);
                            }
                        }
                    }
                    if( toStart != null )
                    {
                        using( monitor.OpenInfo( $"Starting {toStart.Count} new devices." ) )
                        {
                            DeviceApplyConfigurationResult r;
                            foreach( var (idx, d) in toStart )
                            {
                                if( await d.StartAsync( monitor ) )
                                {
                                    r = DeviceApplyConfigurationResult.CreateAndStartSucceeded;
                                }
                                else
                                {
                                    r = DeviceApplyConfigurationResult.CreateSucceededButStartFailed;
                                    success = false;
                                }
                                results[idx] = r;
                                _reconfiguringDevices.Add( d.Name, new ConfiguredDevice<T, TConfiguration>( (T)d, configuration.Items[idx], r ) );
                                _reconfiguringDevicesChanged = true;
                            }
                        }
                    }
                    // Settling.
                    lock( _reconfigureSyncLock )
                    {
                        _devices = _reconfiguringDevices;
                        _reconfiguringDevices = null;
                    }
                    return new ConfigurationResult( success, configuration, results, toDestroy );
                }
                finally
                {
                    _applyConfigAsynclock.Leave( monitor );
                    if( _reconfiguringDevicesChanged )
                    {
                        await RaiseDevicesChangedEvent( monitor ).ConfigureAwait( false );
                    }
                }
            }
        }

        Task RaiseDevicesChangedEvent( IActivityMonitor monitor ) => _devicesChanged.SafeRaiseAsync( monitor, this );
        Task IInternalDeviceHost.RaiseDevicesChangedEvent( IActivityMonitor monitor ) => RaiseDevicesChangedEvent( monitor );

        /// <inheritdoc />
        public Task ClearAsync( IActivityMonitor monitor )
        {
            THostConfiguration hostConfig = _hostConfigFactory();
            hostConfig.IsPartialConfiguration = false;
            return ApplyConfigurationAsync( monitor, hostConfig, true );
        }

        T? SafeCreateDevice( IActivityMonitor monitor, TConfiguration config )
        {
            try
            {
                var device = CreateDevice( monitor, config );
                if( device != null && device.Name != config.Name )
                {
                    monitor.Error( $"Created device name mismatch expected '{config.Name}' got '{device.Name}'." );
                    return null;
                }
                return device;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While trying to instantiate a device from {config.GetType().Name}.", ex );
                return null;
            }
        }

        /// <summary>
        /// Helper that looks for a type from the same namespace and assembly than the <paramref name="typeConfiguration"/>:
        /// the type configuration name must end with "Configuration" and the device must be the same name without this "Configuration" suffix
        /// (or with a "Device" suffix).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="typeConfiguration">The configuration's type.</param>
        /// <returns>The device type or null if not found.</returns>
        protected virtual Type? FindDeviceTypeByConvention( IActivityMonitor monitor, Type typeConfiguration )
        {
            var name = typeConfiguration.Name;
            if( !name.EndsWith( "Configuration" ) )
            {
                monitor.Error( $"Configuration's type name should end with \"Configuration\" suffix." );
                return null;
            }
            Debug.Assert( "Configuration".Length == 13 );
            var fullName = typeConfiguration.FullName;
            var deviceFullName = fullName.Substring( 0, fullName.Length - 13 );
            try
            {
                // Parameter throwOnError doesn't guaranty that NO exception is thrown.
                return typeConfiguration.Assembly.GetType( deviceFullName, throwOnError: false )
                        ?? typeConfiguration.Assembly.GetType( deviceFullName + "Device", throwOnError: false );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While looking for type: '{deviceFullName}' or '{deviceFullName}Device'.", ex );
            }
            return null;
        }

        /// <summary>
        /// Helper that uses <see cref="Activator.CreateInstance(Type, object[])"/> with the <paramref name="monitor"/> and the <paramref name="config"/>
        /// as the constructor parameters.
        /// </summary>
        /// <param name="tDevice">The device type to instantiate.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The device's configuration.</param>
        /// <returns>The new device instance.</returns>
        protected virtual T InstantiateDevice( Type tDevice, IActivityMonitor monitor, TConfiguration config )
        {
            return (T)Activator.CreateInstance( tDevice, new object[] { monitor, CreateCreateInfo( config ) } );
        }

        /// <summary>
        /// Helper that creates a <see cref="Device{TConfiguration}.CreateInfo"/> token.
        /// </summary>
        /// <param name="config">The device's configuration.</param>
        /// <returns>The create information.</returns>
        protected Device<TConfiguration>.CreateInfo CreateCreateInfo( TConfiguration config )
        {
            return new Device<TConfiguration>.CreateInfo( config, this );
        }

        /// <summary>
        /// Creates a <typeparamref name="T"/> device based on a <typeparamref name="TConfiguration"/> instance.
        /// This default implementation uses the protected virtual <see cref="FindDeviceTypeByConvention(IActivityMonitor, Type)"/> to
        /// locate the device type (based on <typeparamref name="TConfiguration"/> first and then the actual <paramref name="config"/>'s type)
        /// and then (if found) instantiates it by calling the other helper <see cref="InstantiateDevice(Type, IActivityMonitor, TConfiguration)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>A new device instance initialized with the <paramref name="config"/> or null on error.</returns>
        protected virtual T? CreateDevice( IActivityMonitor monitor, TConfiguration config )
        {
            Debug.Assert( !String.IsNullOrWhiteSpace( config.Name ), "Already tested by DeviceHostConfiguration<TConfiguration>.CheckValidity." );
            Debug.Assert( !_devices.ContainsKey( config.Name ), "Already tested by DeviceHostConfiguration<TConfiguration>.CheckValidity." );

            var tDevice = FindDeviceTypeByConvention( monitor, typeof( TConfiguration ) );
            if( tDevice == null )
            {
                var actualType = config.GetType();
                monitor.Warn( $"Device type lookup based on TConfiguration interface type failed (formal type). Trying based on actual type '{actualType}'." );
                tDevice = FindDeviceTypeByConvention( monitor, actualType );
                if( tDevice == null )
                {
                    monitor.Error( $"Unable to locate a Device type based on the configuration type." );
                    return null;
                }
            }
            return InstantiateDevice( tDevice, monitor, config );
        }

        /// <summary>
        /// Called when a device has been removed (its configuration disappeared or <see cref="IDevice.DestroyAsync(IActivityMonitor)"/> has been called).
        /// There is no way to prevent the device to be destroyed when its configuration disappeared and this is by design.
        /// This method does nothing at this level.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="device">The device that has been removed.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnDeviceDestroyedAsync( IActivityMonitor monitor, T device ) => Task.CompletedTask;

        /// <inheritdoc />
        public DeviceHostCommandResult SendCommand( IActivityMonitor monitor, BaseDeviceCommand command, bool checkControllerKey = true, CancellationToken token = default )
        {
            var (status, device) = ValidAndRouteCommand( monitor, command );
            if( status != DeviceHostCommandResult.Success ) return status;
            Debug.Assert( device != null );
            if( !device.SendRoutedCommand( command, token, checkControllerKey ) )
            {
                return DeviceHostCommandResult.DeviceDestroyed;
            }
            return DeviceHostCommandResult.Success;
        }


        /// <summary>
        /// Helper that checks and routes a command to its device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command.</param>
        /// <returns>The status and the device if found.</returns>
        protected (DeviceHostCommandResult,T?) ValidAndRouteCommand( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( GetType() ) ) return (DeviceHostCommandResult.InvalidHostType, null);
            if( !command.CheckValidity( monitor ) ) return (DeviceHostCommandResult.CommandCheckValidityFailed, null );

            Debug.Assert( command.DeviceName != null, "CheckValidity ensured that." );
            var d = Find( command.DeviceName );
            if( d == null )
            {
                monitor.Warn( $"Device named '{command.DeviceName}' not found in '{DeviceHostName}' host." );
                return (DeviceHostCommandResult.DeviceNameNotFound, d);
            }
            return (DeviceHostCommandResult.Success, d);
        }

    }


}
