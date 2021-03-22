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
        /// This lock uses the NoRecursion policy. 
        /// </summary>
        readonly AsyncLock _lock;
        readonly PerfectEventSender<IDeviceHost> _devicesChanged;

        // Compile cached lambda.
        readonly Func<THostConfiguration> _hostConfigFactory;

        // This is the whole state of this Host. It is updated atomically (by setting a
        // new dictionary instance).
        Dictionary<string, ConfiguredDevice<T, TConfiguration>> _devices;

        /// <summary>
        /// Initializes a new host.
        /// </summary>
        /// <param name="deviceHostName">A name that SHOULD identify this host instance unambiguously in a running context.</param>
        /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
        protected DeviceHost( string deviceHostName, IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : this( true, alwaysRunningPolicy )
        {
            if( String.IsNullOrWhiteSpace( deviceHostName ) ) throw new ArgumentException( nameof( deviceHostName ) );
            _lock = new AsyncLock( LockRecursionPolicy.NoRecursion, deviceHostName );
        }

        /// <summary>
        /// Initializes a new host with a <see cref="DeviceHostName"/> sets to its type name.
        /// </summary>
        /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
        protected DeviceHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : this( true, alwaysRunningPolicy )
        {
            _lock = new AsyncLock( LockRecursionPolicy.NoRecursion, GetType().Name );
        }

        // This is the only CS8618 warning that must be raised here: Non-nullable field '_lock' is uninitialized.
        DeviceHost( bool privateCall, IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
        {
            _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
            _devicesChanged = new PerfectEventSender<IDeviceHost>();
            _alwaysRunningPolicy = alwaysRunningPolicy;

            var t = typeof( THostConfiguration );
            var ctor = t.GetConstructor( Type.EmptyTypes );
            if( ctor == null ) throw new InvalidOperationException( $"Type '{t.Name}' must have a default public constructor." );
            var m = new DynamicMethod( "CreateInstance", t, Type.EmptyTypes, true );
            ILGenerator ilGenerator = m.GetILGenerator();
            ilGenerator.Emit( OpCodes.Newobj, ctor );
            ilGenerator.Emit( OpCodes.Ret );
            _hostConfigFactory = (Func<THostConfiguration>)m.CreateDelegate( typeof( Func<THostConfiguration> ) );
            _alwayRunningStopped = new List<(IDevice Device, int Count, DateTime NextCall)>();
            _alwayRunningStoppedSafe = Array.Empty<(IDevice, int, DateTime)>();
        }

        /// <inheritdoc />
        public string DeviceHostName => _lock.Name;

        /// <inheritdoc />
        public int Count => _devices.Count;

        Type IDeviceHost.GetDeviceHostConfigurationType() => typeof( THostConfiguration );

        Type IDeviceHost.GetDeviceConfigurationType() => typeof( TConfiguration );

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        public T? this[string deviceName] => Find( deviceName );

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        public T? Find( string deviceName ) => _devices.GetValueOrDefault( deviceName ).Device;

        IDevice? IDeviceHost.Find( string deviceName ) => Find( deviceName );

        /// <summary>
        /// Gets a device and its applied configuration by its name.
        /// See <see cref="ConfiguredDevice{T, TConfiguration}.Configuration"/>.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device and its configuration or null if not found.</returns>
        public ConfiguredDevice<T, TConfiguration>? GetConfiguredDevice( string deviceName )
        {
            return _devices.TryGetValue( deviceName, out var e ) ? e : (ConfiguredDevice<T, TConfiguration>?)null;
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
            readonly IReadOnlyCollection<string>? _unconfiguredDeviceNames;

            /// <summary>
            /// Error constructor: only the initial configuration is provided.
            /// </summary>
            /// <param name="initialConfiguration">The configuration.</param>
            internal ConfigurationResult( THostConfiguration initialConfiguration )
            {
                Success = false;
                HostConfiguration = initialConfiguration;
                var r = new DeviceApplyConfigurationResult[initialConfiguration.Items.Count];
                Array.Fill( r, DeviceApplyConfigurationResult.InvalidHostConfiguration );
                Results = r;
                _unconfiguredDeviceNames = null;
            }

            internal ConfigurationResult( bool success, THostConfiguration initialConfiguration, DeviceApplyConfigurationResult[] r, HashSet<string> unconfiguredNames )
            {
                Success = success;
                HostConfiguration = initialConfiguration;
                Results = r;
                _unconfiguredDeviceNames = unconfiguredNames;
            }

            /// <summary>
            /// Gets whether the configuration of the host succeeded.
            /// </summary>           
            public bool Success { get; }

            /// <summary>
            /// Gets whether the error is due to an invalid or rejected <see cref="HostConfiguration"/>
            /// (detailed <see cref="Results"/> for each device is null in such case).
            /// </summary>
            public bool InvalidHostConfiguration => !Success && _unconfiguredDeviceNames == null;

            /// <summary>
            /// Gets the original configuration.
            /// </summary>
            public THostConfiguration HostConfiguration { get; }

            /// <summary>
            /// Gets the detailed results for each <see cref="IDeviceHostConfiguration.Items"/>.
            /// If <see cref="InvalidHostConfiguration"/> is true, they are all <see cref="DeviceApplyConfigurationResult.InvalidHostConfiguration"/>.
            /// </summary>
            public IReadOnlyList<DeviceApplyConfigurationResult> Results { get; }

            /// <summary>
            /// Gets the device names that have been left as-is if <see cref="IDeviceHostConfiguration.IsPartialConfiguration"/> is true
            /// or have been destroyed if IsPartialConfiguration is false.
            /// </summary>
            public IReadOnlyCollection<string> UnconfiguredDeviceNames => _unconfiguredDeviceNames ?? Array.Empty<string>();
        }

        Task<DeviceApplyConfigurationResult> IDeviceHost.ApplyDeviceConfigurationAsync( IActivityMonitor monitor, DeviceConfiguration configuration )
        {
            return ApplyDeviceConfigurationAsync( monitor, (TConfiguration)configuration );
        }

        /// <summary>
        /// Applies a device configuration: this ensures that the device exists (it is created if needed) and is
        /// configured by the provided <paramref name="configuration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <returns>the result of the device configuration.</returns>
        public async Task<DeviceApplyConfigurationResult> ApplyDeviceConfigurationAsync( IActivityMonitor monitor, TConfiguration configuration )
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

            var safeConfig = configuration.Clone();
            if( !safeConfig.CheckValidity( monitor, allowEmptyConfiguration ) ) return new ConfigurationResult( configuration );

            List<Func<Task>>? postLockActions = null;
            bool success = true;
            using( monitor.OpenInfo( $"Reconfiguring '{DeviceHostName}'. Applying {safeConfig.Items.Count} device configurations." ) )
            {
                // We track the changes: if nothing changed, we don't raise the Changed event.
                // Note that the Changed event is raised outside of the lock.
                bool somethingChanged = false;

                await _lock.EnterAsync( monitor ).ConfigureAwait( false );
                try
                {
                    // Captures and works on a copy of the _devices dictionary (the lock is taken): Find() can work lock-free on _devices.
                    var newDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                    monitor.Trace( $"Starting reconfiguration of {newDevices.Count} devices." );

                    // Safe call to OnBeforeApplyConfigurationAsync.
                    try
                    {
                        if( !await OnBeforeApplyConfigurationAsync( monitor, newDevices, safeConfig ).ConfigureAwait( false ) )
                        {
                            monitor.Debug( $"OnBeforeApplyConfigurationAsync returned false: aborting configuration." );
                            return new ConfigurationResult( configuration );
                        }
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( $"OnBeforeApplyConfigurationAsync error. Aborting configuration.", ex );
                        return new ConfigurationResult( configuration );
                    }

                    // Applying configurations by reconfiguring existing and creating new devices.
                    DeviceApplyConfigurationResult[] results = new DeviceApplyConfigurationResult[safeConfig.Items.Count];
                    var currentDeviceNames = new HashSet<string>( newDevices.Keys );
                    int configIdx = 0;
                    foreach( var c in safeConfig.Items )
                    {
                        DeviceApplyConfigurationResult r;
                        if( !newDevices.TryGetValue( c.Name, out var configured ) )
                        {
                            using( monitor.OpenTrace( $"Creating new device '{c.Name}'." ) )
                            {
                                var d = SafeCreateDevice( monitor, c );
                                Debug.Assert( d == null || d.Name == c.Name );
                                if( d == null )
                                {
                                    r = DeviceApplyConfigurationResult.CreateFailed;
                                    success = false;
                                }
                                else
                                {
                                    r = DeviceApplyConfigurationResult.CreateSucceeded;
                                    newDevices.Add( c.Name, new ConfiguredDevice<T, TConfiguration>( d, c ) );
                                    // A new device has been added to the newDevices.
                                    somethingChanged = true;
                                    if( c.Status == DeviceConfigurationStatus.RunnableStarted || c.Status == DeviceConfigurationStatus.AlwaysRunning )
                                    {
                                        monitor.Trace( $"Starting device because Status is {c.Status}." );
                                        if( !await d.HostStartAsync( monitor, c.Status == DeviceConfigurationStatus.RunnableStarted
                                                                                ? DeviceStartedReason.StartedByRunnableStartedConfiguration
                                                                                : DeviceStartedReason.StartedByAlwaysRunningConfiguration ).ConfigureAwait( false ) )
                                        {
                                            r = DeviceApplyConfigurationResult.CreateSucceededButStartFailed;
                                            success = false;
                                        }
                                        else
                                        {
                                            r = DeviceApplyConfigurationResult.CreateAndStartSucceeded;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using( monitor.OpenTrace( $"Reconfiguring device '{c.Name}'." ) )
                            {
                                currentDeviceNames.Remove( c.Name );
                                Func<Task>? raiseControllerKeyChanged;
                                (r, raiseControllerKeyChanged) = await configured.Device.HostReconfigureAsync( monitor, c ).ConfigureAwait( false );
                                somethingChanged = r != DeviceApplyConfigurationResult.None;
                                // Always updates the configuration.
                                newDevices[c.Name] = new ConfiguredDevice<T, TConfiguration>( configured.Device, c );
                                // Raising the ControllerKeyChanged is done outside of the lock. 
                                if( raiseControllerKeyChanged != null )
                                {
                                    if( postLockActions == null ) postLockActions = new List<Func<Task>>();
                                    postLockActions.Add( raiseControllerKeyChanged );
                                    Debug.Assert( r != DeviceApplyConfigurationResult.None );
                                }
                            }
                            success &= (r == DeviceApplyConfigurationResult.UpdateSucceeded || r == DeviceApplyConfigurationResult.None);
                        }
                        results[configIdx++] = r;
                    }
                    // Now handling device destruction if configuration is a full one.
                    if( configuration.IsPartialConfiguration )
                    {
                        monitor.Debug( $"Configuration is partial: ignored {currentDeviceNames.Count} devices: {currentDeviceNames.Concatenate()}." );
                    }
                    else
                    {
                        using( monitor.OpenInfo( $"Applying full configuration: destroying {currentDeviceNames.Count} devices with no more associated configuration." ) )
                        {
                            foreach( var noMore in currentDeviceNames )
                            {
                                var e = newDevices[noMore];
                                newDevices.Remove( noMore );
                                await DestroyDeviceAsync( monitor, e, false ).ConfigureAwait( false );
                                somethingChanged = true;
                            }
                        }
                    }
                    // We always keep the "newDevices" with their updated configurations: even when a device returned DeviceReconfiguredResult.None,
                    // the configurations are up to date.
                    _devices = newDevices;
                    return new ConfigurationResult( success, configuration, results, currentDeviceNames );
                }
                finally
                {
                    _lock.Leave( monitor );
                    if( somethingChanged )
                    {
                        await SafeRaiseDevicesChanged( monitor ).ConfigureAwait( false );
                    }
                    if( postLockActions != null )
                    {
                        foreach( var a in postLockActions ) await a().ConfigureAwait( false );
                    }
                }
            }
        }

        async Task SafeRaiseDevicesChanged( IActivityMonitor monitor )
        {
            using( monitor.OpenTrace( $"Device Host '{DeviceHostName}' is raising DevicesChanged event." ) )
            {
                await _devicesChanged.SafeRaiseAsync( monitor, this ).ConfigureAwait( false );
            }
        }

        /// <inheritdoc />
        public async Task DestroyDeviceAsync( IActivityMonitor monitor, string deviceName )
        {
            // The Changed event is raised outside of the lock.
            bool somethingChanged = false;
            await _lock.EnterAsync( monitor ).ConfigureAwait( false );
            try
            {
                if( _devices.TryGetValue( deviceName, out var e ) )
                {
                    using( monitor.OpenInfo( $"Destroying {deviceName} device." ) )
                    {
                        var newDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                        newDevices.Remove( e.Device.Name );
                        await DestroyDeviceAsync( monitor, e, false ).ConfigureAwait( false );
                        somethingChanged = true;
                        _devices = newDevices;
                    }
                }
            }
            finally
            {
                _lock.Leave( monitor );
                if( somethingChanged )
                {
                    await SafeRaiseDevicesChanged( monitor ).ConfigureAwait( false );
                }
            }
        }

        /// <inheritdoc />
        public async Task ClearAsync( IActivityMonitor monitor )
        {
            await _lock.EnterAsync( monitor ).ConfigureAwait( false );
            try
            {
                using( monitor.OpenInfo( $"Closing '{DeviceHostName}': stopping {_devices.Count} devices." ) )
                {
                    foreach( var e in _devices.Values )
                    {
                        await DestroyDeviceAsync( monitor, e, false ).ConfigureAwait( false );
                    }
                    _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
                }
            }
            finally
            {
                _lock.Leave( monitor );
            }
        }

        async Task DestroyDeviceAsync( IActivityMonitor monitor, ConfiguredDevice<T,TConfiguration> e, bool autoDestroy )
        {
            using( monitor.OpenInfo( $"Destroying device '{e.Device.FullName}'." ) )
            {
                if( e.Device.IsRunning )
                {
                    if( autoDestroy )
                    {
                        await e.Device.HostStopAsync( monitor, DeviceStoppedReason.AutoDestroyed ).ConfigureAwait( false );
                    }
                    else
                    {
                        await e.Device.HostStopForDestroyOnCommandLoopAsync().ConfigureAwait( false );
                    }
                    Debug.Assert( !e.Device.IsRunning );
                }
                try
                {
                    if( autoDestroy )
                    {
                        await e.Device.HostDestroyAsync( monitor, autoDestroy ).ConfigureAwait( false );
                    }
                    else
                    {
                        await e.Device.HostDestroyOnCommandLoopAsync().ConfigureAwait( false );
                    }
                }
                catch( Exception ex )
                {
                    monitor.Warn( $"'{e.Device.FullName}'.OnDestroyAsync error. This is ignored.", ex );
                }
                await e.Device.HostRaiseDestroyStatusAsync( monitor, autoDestroy ).ConfigureAwait( false );
                try
                {
                    await OnDeviceDestroyedAsync( monitor, e.Device, e.Configuration ).ConfigureAwait( false );
                }
                catch( Exception ex )
                {
                    monitor.Warn( $"'{e.Device.FullName}'.OnDeviceDestroyedAsync error. This is ignored.", ex );
                }
            }
        }

        /// <summary>
        /// Called before a new <paramref name="configuration"/> must be applied to <paramref name="currentDevices"/>.
        /// This happens inside the internal lock and both parameters can be mutated as needed (this is weird, but this is
        /// an extension point anyway).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="currentDevices">Current devices and their configurations that will be updated.</param>
        /// <param name="configuration">The configuration that will be applied.</param>
        /// <returns>True if no error occurred. False to totally cancel the configuration.</returns>
        protected virtual Task<bool> OnBeforeApplyConfigurationAsync( IActivityMonitor monitor, Dictionary<string, ConfiguredDevice<T, TConfiguration>> currentDevices, THostConfiguration configuration )
        {
            return Task.FromResult( true );
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
        /// Called when a device has been removed (its configuration disappeared).
        /// There is no way to prevent the device to be removed when its configuration disappeared and this is by design.
        /// This method does nothing at this level.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="device">The device that has been removed.</param>
        /// <param name="configuration">The device's configuration (before its destruction).</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnDeviceDestroyedAsync( IActivityMonitor monitor, T device, TConfiguration configuration ) => Task.CompletedTask;

        /// <inheritdoc />
        public DeviceHostCommandResult SendCommand( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token = default )
        {
            var (status, device) = RouteCommand( monitor, command );
            if( status != DeviceHostCommandResult.Success ) return status;
            Debug.Assert( device != null );
            if( !device.SendRoutedCommand( command, token ) )
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
        protected (DeviceHostCommandResult,T?) RouteCommand( IActivityMonitor monitor, DeviceCommandBase command )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( GetType() ) ) return (DeviceHostCommandResult.InvalidHostType, null);
            if( command.GetCompletionResult().IsCompleted ) return (DeviceHostCommandResult.CommandAlreadyUsed, null);
            if( !command.CheckValidity( monitor ) ) return (DeviceHostCommandResult.CommandCheckValidityFailed, null );

            Debug.Assert( command.DeviceName != null, "CheckValidity ensured that." );
            var d = Find( command.DeviceName );
            if( d == null )
            {
                monitor.Warn( $"Device named '{command.DeviceName}' not found in '{DeviceHostName}' host." );
                return (DeviceHostCommandResult.DeviceNameNotFound, d);
            }
            var invalidKey = d.CheckCommandControllerKey( command );
            if( invalidKey != null )
            {
                monitor.Warn( $"Command skipped by host '{DeviceHostName}': {invalidKey}." );
                return (DeviceHostCommandResult.ControllerKeyMismatch, d);
            }
            return (DeviceHostCommandResult.Success, d);
        }

        enum DeviceAction
        {
            Start,
            Stop,
            AutoStop,
            AutoStart,
            AutoStopForce,
            AutoDestroy
        }

        Task<bool> IInternalDeviceHost.StartAsync( IDevice d, IActivityMonitor monitor, bool autoStart ) => DeviceActionAsync( d, monitor, autoStart ? DeviceAction.AutoStart : DeviceAction.Start );

        Task<bool> IInternalDeviceHost.StopAsync( IDevice d, IActivityMonitor monitor ) => DeviceActionAsync( d, monitor, DeviceAction.Stop );

        Task<bool> IInternalDeviceHost.AutoStopAsync( IDevice d, IActivityMonitor monitor, bool ignoreAlwaysRunning ) => DeviceActionAsync( d, monitor, ignoreAlwaysRunning ? DeviceAction.AutoStopForce : DeviceAction.AutoStop );

        Task IInternalDeviceHost.AutoDestroyAsync( IDevice d, IActivityMonitor monitor ) => DeviceActionAsync( d, monitor, DeviceAction.AutoDestroy );

        async Task<bool> DeviceActionAsync( IDevice d, IActivityMonitor monitor, DeviceAction a )
        {
            bool success;
            await _lock.EnterAsync( monitor ).ConfigureAwait( false );
            // Nothing can throw here: avoid useless try/catch.
            if( _devices.TryGetValue( d.Name, out var e ) )
            {
                bool raiseChanged = false;
                switch( a )
                {
                    case DeviceAction.Start: success = e.Device.IsRunning || await e.Device.HostStartAsync( monitor, DeviceStartedReason.StartCall ).ConfigureAwait( false ); break;
                    case DeviceAction.Stop: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.StoppedCall ).ConfigureAwait( false ); break;
                    case DeviceAction.AutoStart: success = e.Device.IsRunning || await e.Device.HostStartAsync( monitor, DeviceStartedReason.AutoStart ).ConfigureAwait( false ); break;
                    case DeviceAction.AutoStop: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.AutoStoppedCall ).ConfigureAwait( false ); break;
                    case DeviceAction.AutoStopForce: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.AutoStoppedForceCall ).ConfigureAwait( false ); break;
                    case DeviceAction.AutoDestroy:
                        {
                            var newDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                            await DestroyDeviceAsync( monitor, e, true ).ConfigureAwait( false );
                            newDevices.Remove( d.Name );
                            success = raiseChanged = true;
                            _devices = newDevices;
                            break;
                        }
                    default: throw new NotImplementedException();
                }
                _lock.Leave( monitor );
                if( raiseChanged )
                {
                    await SafeRaiseDevicesChanged( monitor ).ConfigureAwait( false );
                }
            }
            else
            {
                _lock.Leave( monitor );
                if( a == DeviceAction.Start )
                {
                    monitor.Error( $"Attempt to Start a detached device '{d.FullName}'." );
                    success = false;
                }
                else
                {
                    monitor.Warn( $"Attempt to {a} a detached device '{d.FullName}'." );
                    success = true;
                }
            }
            return success;
        }

        async Task<bool> IInternalDeviceHost.SetControllerKeyAsync( IDevice d, IActivityMonitor monitor, bool checkCurrent, string? current, string? key )
        {
            bool success;
            await _lock.EnterAsync( monitor ).ConfigureAwait( false );
            // Nothing can throw here: avoid useless try/catch.
            if( _devices.TryGetValue( d.Name, out var e ) )
            {
                var sender = e.Device.HostSetControllerKey( monitor, checkCurrent, current, key );
                _lock.Leave( monitor );
                success = sender != null;
                if( success )
                {
                    await sender!.RaiseAsync( monitor, e.Device, key ).ConfigureAwait( false );
                }
            }
            else
            {
                _lock.Leave( monitor );
                success = Device<TConfiguration>.SetControllerKeyOnDetachedAsync( monitor, key, d );
            }
            return success;
        }

    }


}
