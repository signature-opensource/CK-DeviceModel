using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel;

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
    where TConfiguration : DeviceConfiguration, new()
{
    /// <summary>
    /// This is the whole state of this Host. It is updated atomically (by setting a
    /// new dictionary instance). All Find methods (and RouteCommand) can use it lock-free.
    /// It is exposed by GetDevices() methods.
    /// </summary>
    Dictionary<string, T> _devices;

    readonly PerfectEventSender<IDeviceHost, IReadOnlyDictionary<string, IDevice>> _baseDevicesChanged;
    readonly PerfectEventSender<IDeviceHost, IReadOnlyDictionary<string, T>> _devicesChanged;
    readonly PerfectEventSender<IDeviceHost, DeviceLifetimeEvent> _allDevicesLifetimeEvent;
    readonly PerfectEventSender<IDeviceHost, BaseDeviceEvent> _allDevicesEvent;


    /// <summary>
    /// This lock uses the NoRecursion policy.
    /// It protects the whole ApplyConfigurationAsync: only one global reconfiguration is
    /// allowed at a time.
    /// Reconfigurations or destruction can concurrently happen when the IDevice methods are used.
    /// </summary>
    readonly AsyncLock _applyConfigAsyncLock;

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
    /// The device's DestroyAsync method calls the synchronous OnDeviceDestroyed.
    /// OnDeviceDestroyed enters the _reconfigureSyncLock and updates the _reconfiguringDevices field
    /// if it's not null or creates a new dictionary (copying the _devices), update it and set it as the new _devices.
    /// </para>
    /// <para>
    /// Once ApplyConfigurationAsync has called all required DestroyAsync methods,
    /// it enters the _reconfigureSyncLock for the last time to set the _devices to the _reconfiguringDevices
    /// and to reset the _reconfiguringDevices to null. It finally returns the array of DeviceConfigurationResult.
    /// </para>
    /// </summary>
    Dictionary<string, T>? _reconfiguringDevices;
    bool _reconfiguringDevicesChanged;
    readonly object _reconfigureSyncLock;

    /// <summary>
    /// Initializes a new host.
    /// </summary>
    /// <param name="deviceHostName">A name that SHOULD identify this host instance unambiguously in a running context.</param>
    protected DeviceHost( string deviceHostName )
        : this( true )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( deviceHostName );
        // The name of the lock is the DeviceHostName.
        _applyConfigAsyncLock = new AsyncLock( LockRecursionPolicy.NoRecursion, deviceHostName );
    }

    /// <summary>
    /// Initializes a new host with a <see cref="DeviceHostName"/> sets to its simple type name
    /// (that should end, by convention, with "Host").
    /// </summary>
    protected DeviceHost()
        : this( true )
    {
        _applyConfigAsyncLock = new AsyncLock( LockRecursionPolicy.NoRecursion, GetType().Name );
    }

    DeviceHost( bool privateCall )
    {
        _devices = new Dictionary<string, T>();
        _baseDevicesChanged = new PerfectEventSender<IDeviceHost, IReadOnlyDictionary<string, IDevice>>();
        _devicesChanged = new PerfectEventSender<IDeviceHost, IReadOnlyDictionary<string, T>>();
        _allDevicesLifetimeEvent = new PerfectEventSender<IDeviceHost, DeviceLifetimeEvent>();
        _allDevicesEvent = new PerfectEventSender<IDeviceHost, BaseDeviceEvent>();

        _devicesChanged.CreateBridge( _baseDevicesChanged, e =>
        {
            // CK.Core v16.0.0 has not the AsIReadOnlyDictionary on IReadOnlyDictionary.
            // This has been fixed in subsequent versions.
            return ((Dictionary<string, T>)e).AsIReadOnlyDictionary<string, T, IDevice>();
        } );

        // Generates a typed delegate to instantiate the THostConfiguration dynamically used
        // to apply a partial configuration.
        var t = typeof( THostConfiguration );
        var ctor = t.GetConstructor( Type.EmptyTypes );
        if( ctor == null ) throw new InvalidOperationException( $"Type '{t.Name}' must have a default public constructor." );
        var m = new DynamicMethod( "CreateInstance", t, Type.EmptyTypes, true );
        ILGenerator ilGenerator = m.GetILGenerator();
        ilGenerator.Emit( OpCodes.Newobj, ctor );
        ilGenerator.Emit( OpCodes.Ret );
        _hostConfigFactory = (Func<THostConfiguration>)m.CreateDelegate( typeof( Func<THostConfiguration> ) );

        _reconfigureSyncLock = new DeviceHostLock();
        _alwayRunningStopped = new List<(IInternalDevice Device, int Count, DateTime NextCall)>();
        // Shut up the CS8618 warning is raised here: Non-nullable field '_applyConfigAsynclock' is uninitialized.
        // (But keep the warning for any other fields.)
        _applyConfigAsyncLock = null!;
    }

    /// <inheritdoc />
    public string DeviceHostName => _applyConfigAsyncLock.Name;

    /// <inheritdoc />
    public int Count => _devices.Count;

    Type IDeviceHost.GetDeviceHostConfigurationType() => typeof( THostConfiguration );

    Type IDeviceHost.GetDeviceConfigurationType() => typeof( TConfiguration );

    BaseConfigureDeviceCommand IInternalDeviceHost.CreateLockedConfigureCommand( string name,
                                                                                 string? controllerKey,
                                                                                 DeviceConfiguration? externalConfiguration,
                                                                                 DeviceConfiguration? clonedConfig )
    {
        return new InternalConfigureDeviceCommand<TConfiguration>( GetType(), externalConfiguration, clonedConfig, (name, controllerKey) );
    }

    BaseStartDeviceCommand IInternalDeviceHost.CreateStartCommand( string name ) => new InternalStartDeviceCommand( GetType(), name );

    BaseStopDeviceCommand IInternalDeviceHost.CreateStopCommand( string name, bool ignoreAlwaysRunning ) => new InternalStopDeviceCommand( GetType(), name, ignoreAlwaysRunning );

    BaseDestroyDeviceCommand IInternalDeviceHost.CreateDestroyCommand( string name ) => new InternalDestroyDeviceCommand( GetType(), name );

    BaseSetControllerKeyDeviceCommand IInternalDeviceHost.CreateSetControllerKeyDeviceCommand( string name, string? current, string? newControllerKey ) => new InternalSetControllerKeyDeviceCommand( GetType(), name, current, newControllerKey );

    bool IInternalDeviceHost.OnDeviceDoDestroy( IActivityMonitor monitor, IDevice device )
    {
        lock( _reconfigureSyncLock )
        {
            if( _reconfiguringDevices != null )
            {
                _reconfiguringDevices.Remove( device.Name );
                _reconfiguringDevicesChanged = true;
                return false;
            }
            var devices = new Dictionary<string, T>( _devices );
            devices.Remove( device.Name );
            _devices = devices;
            // Returning true will call RaiseDevicesChangedEventAsync.
            return true;
        }
    }

    /// <summary>
    /// Gets a device by its name.
    /// <para>
    /// This is efficient since it lookups an independent read only dictionary instance. No lock needed.
    /// </para>
    /// </summary>
    /// <param name="deviceName">The device name to find.</param>
    /// <returns>The device or null if not found.</returns>
    public T? Find( string deviceName ) => _devices.GetValueOrDefault( deviceName );

    /// <inheritdoc cref="Find(string)"/>
    public T? this[string deviceName] => _devices.GetValueOrDefault( deviceName );

    IDevice? IDeviceHost.Find( string deviceName ) => Find( deviceName );


    IReadOnlyDictionary<string, IDevice> IDeviceHost.GetDevices() => _devices.AsIReadOnlyDictionary<string, T, IDevice>();

    /// <summary>
    /// Gets a snapshot of the current devices indexed by name.
    /// This read only dictionary can be freely used (there is no concurrency issues), <see cref="DevicesChanged"/>
    /// event can be used to monitor changes.
    /// </summary>
    /// <returns>A snapshot of the devices.</returns>
    public IReadOnlyDictionary<string, T> GetDevices() => _devices;

    PerfectEvent<IDeviceHost, IReadOnlyDictionary<string, IDevice>> IDeviceHost.DevicesChanged => _baseDevicesChanged.PerfectEvent;

    /// <inheritdoc />
    public PerfectEvent<IDeviceHost, IReadOnlyDictionary<string, T>> DevicesChanged => _devicesChanged.PerfectEvent;

    Task IInternalDeviceHost.RaiseDevicesChangedEventAsync( IActivityMonitor monitor ) => RaiseDevicesChangedEventAsync( monitor );

    Task RaiseDevicesChangedEventAsync( IActivityMonitor monitor ) => DaemonStoppedToken.IsCancellationRequested
                                                                        ? Task.CompletedTask
                                                                        : _devicesChanged.SafeRaiseAsync( monitor,
                                                                                                          this,
                                                                                                          _devices );

    /// <inheritdoc />
    public PerfectEvent<IDeviceHost, DeviceLifetimeEvent> AllDevicesLifetimeEvent => _allDevicesLifetimeEvent.PerfectEvent;

    /// <inheritdoc />
    public PerfectEvent<IDeviceHost, BaseDeviceEvent> AllDevicesEvent => _allDevicesEvent.PerfectEvent;

    Task IInternalDeviceHost.RaiseAllDevicesLifetimeEventAsync( IActivityMonitor monitor, DeviceLifetimeEvent e )
    {
        return DaemonStoppedToken.IsCancellationRequested
                ? Task.CompletedTask
                : _allDevicesLifetimeEvent.SafeRaiseAsync( monitor, this, e );
    }

    Task IInternalDeviceHost.RaiseAllDevicesEventAsync( IActivityMonitor monitor, BaseDeviceEvent e )
    {
        return DaemonStoppedToken.IsCancellationRequested
                ? Task.CompletedTask
                : _allDevicesEvent.SafeRaiseAsync( monitor, this, e );
    }


    /// <summary>
    /// Captures the result of <see cref="ApplyConfigurationAsync"/>.
    /// </summary>
    public readonly struct ConfigurationResult
    {
        readonly IReadOnlyCollection<string>? _destroyedNames;

        /// <summary>
        /// Error constructor: only the initial configuration is provided.
        /// </summary>
        /// <param name="badConfiguration">The configuration.</param>
        internal ConfigurationResult( THostConfiguration badConfiguration )
        {
            Success = false;
            HostConfiguration = badConfiguration;
            var r = new DeviceApplyConfigurationResult[badConfiguration.Items.Count];
            Array.Fill( r, DeviceApplyConfigurationResult.InvalidConfiguration );
            Results = r;
            _destroyedNames = null;
        }

        internal ConfigurationResult( bool success, THostConfiguration initialConfiguration, DeviceApplyConfigurationResult[] r, HashSet<string>? destroyedNames )
        {
            Success = success;
            HostConfiguration = initialConfiguration;
            Results = r;
            _destroyedNames = (IReadOnlyCollection<string>?)destroyedNames ?? Array.Empty<string>();
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
        Throw.CheckNotNullArgument( configuration );

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

    BaseConfigureDeviceCommand IDeviceHost.CreateConfigureCommand( DeviceConfiguration? configuration )
    {
        return new InternalConfigureDeviceCommand<TConfiguration>( GetType(), configuration, null );
    }

    /// <summary>
    /// Applies a host configuration: multiple devices can be configured at once and if <see cref="DeviceHostConfiguration{TConfiguration}.IsPartialConfiguration"/> is false,
    /// devices for which no configuration appear are stopped and destroyed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="allowEmptyConfiguration">By default, an empty configuration is considered as an error.</param>
    /// <returns>The composite result of the potentially multiple configurations.</returns>
    public virtual async Task<ConfigurationResult> ApplyConfigurationAsync( IActivityMonitor monitor, THostConfiguration configuration, bool allowEmptyConfiguration = false )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( configuration );

        using var autoTag = monitor.TemporarilySetAutoTags( IDeviceHost.DeviceModel );

        var safeConfig = configuration.DeepClone();
        if( !safeConfig.CheckValidity( monitor, allowEmptyConfiguration ) ) return new ConfigurationResult( configuration );

        var results = new DeviceApplyConfigurationResult[safeConfig.Items.Count];
        // When the call to the constructor is enough, there will be no DeviceLifetimeEvent emitted.
        // To be able to signal this device creation as a DeviceLifetimeEvent,
        // we capture these beasts and emit a "fake" device event
        // with Status/Configuration/ControllerKeyChanged all set to true for them.
        List<IInternalDevice>? createdDevices = null;

        bool success = true;
        using( monitor.OpenInfo( $"Reconfiguring '{DeviceHostName}'. Applying {safeConfig.Items.Count} device configurations ({(safeConfig.IsPartialConfiguration ? "partial" : "full")} configuration)." ) )
        {
            await _applyConfigAsyncLock.EnterAsync( monitor ).ConfigureAwait( false );
            try
            {
                HashSet<string>? toDestroy = null;
                List<(int, T)>? toStart = null;
                List<(int, T)>? toReconfigure = null;

                // Capturing what we need to be able to work outside the sync lock.
                lock( _reconfigureSyncLock )
                {
                    _reconfiguringDevices = new Dictionary<string, T>( _devices );
                    _reconfiguringDevicesChanged = false;
                    if( !safeConfig.IsPartialConfiguration ) toDestroy = new HashSet<string>( _reconfiguringDevices.Keys );
                    int configIdx = 0;
                    foreach( var c in safeConfig.Items )
                    {
                        if( !_reconfiguringDevices.TryGetValue( c.Name, out var exists ) )
                        {
                            using( monitor.OpenTrace( $"Creating new device '{c.Name}'." ) )
                            {
                                var d = SafeCreateDevice( monitor, c, externalConfig: configuration.Items[configIdx] );
                                Debug.Assert( d == null || d.Name == c.Name );
                                if( d == null )
                                {
                                    results[configIdx] = DeviceApplyConfigurationResult.CreateFailed;
                                    success = false;
                                }
                                else
                                {
                                    if( createdDevices == null ) createdDevices = new List<IInternalDevice>();
                                    createdDevices.Add( d );
                                    if( c.Status == DeviceConfigurationStatus.RunnableStarted || c.Status == DeviceConfigurationStatus.AlwaysRunning )
                                    {
                                        if( toStart == null ) toStart = new List<(int, T)>();
                                        toStart.Add( (configIdx, d) );
                                    }
                                    else
                                    {
                                        _reconfiguringDevices.Add( c.Name, d );
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
                            toReconfigure.Add( (configIdx, exists) );
                        }
                        ++configIdx;
                    }
                }

                // No more in the lock: applies the toDestroy, toReconfigure and eventually toStart lists.
                if( toDestroy != null && toDestroy.Count > 0 )
                {
                    using( monitor.OpenInfo( $"Destroying {toDestroy.Count} devices." ) )
                    {
                        foreach( var n in toDestroy )
                        {
                            var d = Find( n );
                            if( d != null && !d.IsDestroyed )
                            {
                                // We wait for the device to be destroyed here:
                                // resources from a destroyed devices may require an exclusive access
                                // that a reconfigured or started device (handled below) need.
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
                        foreach( var (idx, d) in toReconfigure )
                        {
                            DeviceApplyConfigurationResult r = await d.InternalReconfigureAsync( monitor, configuration.Items[idx], safeConfig.Items[idx], default ).ConfigureAwait( false );
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
                            _reconfiguringDevices.Add( d.Name, d );
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
                _applyConfigAsyncLock.Leave( monitor );
                if( _reconfiguringDevicesChanged )
                {
                    await RaiseDevicesChangedEventAsync( monitor ).ConfigureAwait( false );
                }
                if( createdDevices != null )
                {
                    foreach( var d in createdDevices )
                    {
                        await d.EnsureInitialLifetimeEventAsync( monitor );
                    }
                }
            }
        }
    }



    /// <inheritdoc />
    public async Task ClearAsync( IActivityMonitor monitor, bool waitForDeviceDestroyed )
    {
        using var autoTag = monitor.TemporarilySetAutoTags( IDeviceHost.DeviceModel );

        if( waitForDeviceDestroyed )
        {
            await Task.WhenAll( ParrallelStartDestroy( monitor ) ).ConfigureAwait( false );
        }
        else
        {
            var d = _devices;
            monitor.Info( $"Clearing '{DeviceHostName}': {d.Count} devices will be eventually destroyed." );
            foreach( var device in d.Values )
            {
                await device.DestroyAsync( monitor, waitForDeviceDestroyed: false ).ConfigureAwait( false );
            }
        }
    }

    Task[] ParrallelStartDestroy( IActivityMonitor monitor )
    {
        Type thisType = GetType();
        var d = _devices;
        monitor.Info( $"Clearing '{DeviceHostName}': destroying {d.Count} devices." );
        int i = 0;
        Task[] all = new Task[d.Count];
        foreach( var device in d.Values )
        {
            var cmd = new InternalDestroyDeviceCommand( thisType, device.Name );
            device.UnsafeSendCommand( monitor, cmd );
            all[i++] = cmd.Completion.Task;
        }
        return all;
    }

    T? SafeCreateDevice( IActivityMonitor monitor, TConfiguration config, TConfiguration externalConfig )
    {
        if( DaemonStoppedToken.IsCancellationRequested )
        {
            monitor.Trace( "System is shutting down. Skipping new device creation." );
            return null;
        }
        try
        {
            if( !externalConfig.CheckValid( monitor ) )
            {
                monitor.Error( $"External configuration CheckValid failed but its clone has been validated. Something really weird happens." );
                return null;
            }
            var device = CreateDevice( monitor, config, externalConfig );
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
        Debug.Assert( fullName != null );
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
    /// Helper that uses <see cref="Activator.CreateInstance(Type, object[])"/> with the <paramref name="monitor"/> and the
    /// result of the call to <see cref="CreateCreateInfo(TConfiguration, TConfiguration)"/> with <paramref name="config"/>
    /// and <paramref name="externalConfig"/> as the constructor parameters.
    /// </summary>
    /// <param name="tDevice">The device type to instantiate.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="config">The actual configuration (safe clone).</param>
    /// <param name="externalConfig">The external configuration (original).</param>
    /// <returns>The new device instance.</returns>
    protected virtual T InstantiateDevice( Type tDevice, IActivityMonitor monitor, TConfiguration config, TConfiguration externalConfig )
    {
        return (T)Activator.CreateInstance( tDevice, new object[] { monitor, CreateCreateInfo( config, externalConfig ) } )!;
    }

    /// <summary>
    /// Helper that creates a <see cref="Device{TConfiguration}.CreateInfo"/> opaque object.
    /// </summary>
    /// <param name="config">The actual configuration (safe clone).</param>
    /// <param name="externalConfig">The external configuration (original).</param>
    /// <returns>The create information.</returns>
    protected Device<TConfiguration>.CreateInfo CreateCreateInfo( TConfiguration config, TConfiguration externalConfig )
    {
        return new Device<TConfiguration>.CreateInfo( config, externalConfig, this );
    }

    /// <summary>
    /// Creates a <typeparamref name="T"/> device based on a <typeparamref name="TConfiguration"/> instance.
    /// This default implementation uses the protected virtual <see cref="FindDeviceTypeByConvention(IActivityMonitor, Type)"/> to
    /// locate the device type (based on <typeparamref name="TConfiguration"/> first and then the actual <paramref name="config"/>'s type)
    /// and then (if found) instantiates it by calling the other helper <see cref="InstantiateDevice"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="config">The actual configuration (safe clone).</param>
    /// <param name="externalConfig">The external configuration (original).</param>
    /// <returns>A new device instance initialized with the <paramref name="config"/> or null on error.</returns>
    protected virtual T? CreateDevice( IActivityMonitor monitor, TConfiguration config, TConfiguration externalConfig )
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
        return InstantiateDevice( tDevice, monitor, config, externalConfig );
    }

    /// <inheritdoc />
    public DeviceHostCommandResult SendCommand( IActivityMonitor monitor, BaseDeviceCommand command, bool checkControllerKey = true, CancellationToken token = default )
    {
        var (status, device) = ValidateAndRouteCommand( monitor, command );
        if( status != DeviceHostCommandResult.Success ) return status;
        Debug.Assert( device != null );
        monitor.Trace( IDeviceHost.DeviceModel, $"{DeviceHostName}: sending {(command.ImmediateSending ? "immediate" : "")} '{command}' to '{device.Name}'." );
        if( !(command.ImmediateSending
                ? device.SendRoutedCommandImmediate( command, checkControllerKey, token )
                : device.SendRoutedCommand( command, checkControllerKey, token )) )
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
    protected (DeviceHostCommandResult, T?) ValidateAndRouteCommand( IActivityMonitor monitor, BaseDeviceCommand command )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( command );
        if( !command.HostType.IsAssignableFrom( GetType() ) ) return (DeviceHostCommandResult.InvalidHostType, null);
        if( !command.CheckValidity( monitor ) ) return (DeviceHostCommandResult.CommandCheckValidityFailed, null);
        // Should we do something like this here?
        // command.DependentToken ??= monitor.CreateDependentToken();
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
