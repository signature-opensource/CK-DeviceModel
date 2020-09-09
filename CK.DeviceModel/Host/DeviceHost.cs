using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
    public abstract class DeviceHost<T, THostConfiguration, TConfiguration> : IDeviceHost, IInternalDeviceHost
        where T : Device<TConfiguration>
        where THostConfiguration : DeviceHostConfiguration<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        readonly AsyncLock _lock;

        // This is the whole state of this Host. It is updated atomically (by setting a
        // new dictionary instance).
        Dictionary<string, ConfiguredDevice<T, TConfiguration>> _devices;

        /// <summary>
        /// Initializes a new host.
        /// </summary>
        /// <param name="deviceHostName">A name that SHOULD identify this host instance unabiguously in a running context.</param>
        protected DeviceHost( string deviceHostName )
        {
            if( String.IsNullOrWhiteSpace( deviceHostName ) ) throw new ArgumentException( nameof( deviceHostName ) );
            _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
            _lock = new AsyncLock( LockRecursionPolicy.NoRecursion, deviceHostName );
        }

        /// <summary>
        /// Initializes a new host with a <see cref="DeviceHostName"/> sets to its type name.
        /// </summary>
        protected DeviceHost()
        {
            _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
            _lock = new AsyncLock( LockRecursionPolicy.NoRecursion, GetType().Name );
        }

        /// <summary>
        /// Gets the host name that SHOULD identify this host instance unabiguously in a running context.
        /// Defaults to this concrete <see cref="MemberInfo.Name">type's name</see>.
        /// </summary>
        public string DeviceHostName => _lock.Name;

        /// <summary>
        /// Gets the number of devices.
        /// </summary>
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

        /// <summary>
        /// Gets a device and its configuration by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device and its configuration or null if not found.</returns>
        public ConfiguredDevice<T, TConfiguration>? FindWithConfiguration( string deviceName )
        {
            return _devices.TryGetValue( deviceName, out var e ) ? e : (ConfiguredDevice<T, TConfiguration>?)null;
        }

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
                Results = null;
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
            public bool InvalidHostConfiguration => !Success && Results == null;

            /// <summary>
            /// Gets the original configuration.
            /// </summary>
            public THostConfiguration HostConfiguration { get; }

            /// <summary>
            /// Gets the detailed results for each <see cref="IDeviceHostConfiguration.Items"/>.
            /// This is null if <see cref="InvalidHostConfiguration"/> is true.
            /// </summary>
            public IReadOnlyList<DeviceApplyConfigurationResult>? Results { get; }

            /// <summary>
            /// Gets the device names that have been left as-is if <see cref="IDeviceHostConfiguration.IsPartialConfiguration"/> is true
            /// or have been destroyed if IsPartialConfiguration is false.
            /// </summary>
            public IReadOnlyCollection<string> UnconfiguredDeviceNames => _unconfiguredDeviceNames ?? Array.Empty<string>();
        }


        async Task<bool> IDeviceHost.ApplyConfigurationAsync( IActivityMonitor monitor, IDeviceHostConfiguration configuration, bool allowEmptyConfiguration )
        {
            var r = await ApplyConfigurationAsync( monitor, (THostConfiguration)configuration, allowEmptyConfiguration ).ConfigureAwait( false );
            return r.Success;
        }

        /// <summary>
        /// Applies a host configuration.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <param name="allowEmptyConfiguration">By default, an empty configuration is considered as an error.</param>
        /// <returns>A result.</returns>
        public async Task<ConfigurationResult> ApplyConfigurationAsync( IActivityMonitor monitor, THostConfiguration configuration, bool allowEmptyConfiguration = false )
        {
            if( configuration == null ) throw new ArgumentNullException( nameof( configuration ) );
            var safeConfig = configuration.Clone();
            if( !safeConfig.CheckValidity( monitor, allowEmptyConfiguration ) ) return new ConfigurationResult( configuration );

            bool success = true;
            using( monitor.OpenInfo( $"Reconfiguring '{DeviceHostName}'. Applying {safeConfig.Items.Count} device configurations." ) )
            {
                await _lock.EnterAsync( monitor );
                try
                {
                    // Captures and works on a copy of the _devices dictionary (the lock is taken): Find() can work lock-free on _devices.
                    var newDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                    monitor.Trace( $"Starting reconfiguration of {newDevices.Count} devices." );

                    // Safe call to OnBeforeApplyConfigurationAsync.
                    try
                    {
                        if( !await OnBeforeApplyConfigurationAsync( monitor, newDevices, safeConfig ) )
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
                                    continue;
                                }
                                r = DeviceApplyConfigurationResult.CreateSucceeded;
                                d.HostSetHost( this );
                                newDevices.Add( c.Name, new ConfiguredDevice<T, TConfiguration>( d, c ) );
                                if( c.Status == DeviceConfigurationStatus.RunnableStarted || c.Status == DeviceConfigurationStatus.AlwaysRunning )
                                {
                                    monitor.Trace( $"Starting device since Status = {c.Status}." );
                                    if( !await d.HostStartAsync( monitor, c.Status == DeviceConfigurationStatus.RunnableStarted
                                                                            ? DeviceStartedReason.StartedByRunnableStartedConfiguration
                                                                            : DeviceStartedReason.StartedByAlwaysRunningConfiguration ) )
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
                        else
                        {
                            using( monitor.OpenTrace( $"Reconfiguring device '{c.Name}'." ) )
                            {
                                currentDeviceNames.Remove( c.Name );
                                r = await configured.Device.HostReconfigureAsync( monitor, c ).ConfigureAwait( false );
                            }
                            success &= r == DeviceApplyConfigurationResult.UpdateSucceeded;
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
                                await DestroyDevice( monitor, e ).ConfigureAwait( false );
                            }
                        }
                    }
                    _devices = newDevices;
                    return new ConfigurationResult( success, configuration, results, currentDeviceNames );
                }
                finally
                {
                    _lock.Leave( monitor );
                }
            }
        }

        /// <inheritdoc />
        public async Task ClearAsync( IActivityMonitor monitor )
        {
            await _lock.EnterAsync( monitor );
            try
            {
                using( monitor.OpenInfo( $"Closing '{DeviceHostName}': stopping {_devices.Count} devices." ) )
                {
                    foreach( var e in _devices.Values )
                    {
                        await DestroyDevice( monitor, e );
                    }
                    _devices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>();
                }
            }
            finally
            {
                _lock.Leave( monitor );
            }
        }

        async Task DestroyDevice( IActivityMonitor monitor, ConfiguredDevice<T,TConfiguration> e )
        {
            using( monitor.OpenInfo( $"Destroying device '{e.Device.FullName}'." ) )
            {
                if( e.Device.IsRunning )
                {
                    await e.Device.HostStopAsync( monitor, DeviceStoppedReason.StoppedBeforeDestroy );
                    Debug.Assert( !e.Device.IsRunning );
                }
                try
                {
                    await e.Device.HostDestroyAsync( monitor );
                }
                catch( Exception ex )
                {
                    monitor.Warn( $"'{e.Device.FullName}'.OnDestroyAsync error. This is ignored.", ex );
                }
                try
                {
                    await OnDeviceDestroyedAsync( monitor, e.Device, e.Configuration );
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
                monitor.Error( $"While trying to instanciate a device from {config.GetType().Name}.", ex );
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
            return (T)Activator.CreateInstance( tDevice, new object[] { monitor, config } );
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

        enum DeviceAction
        {
            Start,
            Stop,
            AutoStop,
            AutoStopForce,
            AutoDestroy
        }

        Task<bool> IInternalDeviceHost.StartAsync( IDevice d, IActivityMonitor monitor ) => DeviceActionAsync( d, monitor, DeviceAction.Start );

        Task<bool> IInternalDeviceHost.StopAsync( IDevice d, IActivityMonitor monitor ) => DeviceActionAsync( d, monitor, DeviceAction.Stop );

        Task<bool> IInternalDeviceHost.AutoStopAsync( IDevice d, IActivityMonitor monitor, bool ignoreAlwaysRunning ) => DeviceActionAsync( d, monitor, ignoreAlwaysRunning ? DeviceAction.AutoStop : DeviceAction.AutoStopForce );

        Task IInternalDeviceHost.AutoDestroyAsync( IDevice d, IActivityMonitor monitor ) => DeviceActionAsync( d, monitor, DeviceAction.AutoDestroy );

        async Task<bool> DeviceActionAsync( IDevice d, IActivityMonitor monitor, DeviceAction a )
        {
            bool success;
            await _lock.EnterAsync( monitor );
            // Nothing can throw here: avoid useless try/catch.
            if( _devices.TryGetValue( d.Name, out var e ) )
            {
                switch( a )
                {
                    case DeviceAction.Start: success = e.Device.IsRunning || await e.Device.HostStartAsync( monitor, DeviceStartedReason.StartedCall ); break;
                    case DeviceAction.Stop: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.StoppedCall ); break;
                    case DeviceAction.AutoStop: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.AutoStoppedCall ); break;
                    case DeviceAction.AutoStopForce: success = !e.Device.IsRunning || await e.Device.HostStopAsync( monitor, DeviceStoppedReason.AutoStoppedForceCall ); break;
                    case DeviceAction.AutoDestroy:
                        {
                            var newDevices = new Dictionary<string, ConfiguredDevice<T, TConfiguration>>( _devices );
                            await DestroyDevice( monitor, e );
                            newDevices.Remove( d.Name );
                            success = true;
                            _devices = newDevices;
                            break;
                        }
                    default: throw new NotImplementedException();
                }
                _lock.Leave( monitor );
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

    }


}
