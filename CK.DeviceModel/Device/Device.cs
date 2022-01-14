using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;
using System.Threading.Channels;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for a device.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract partial class Device<TConfiguration> : IDevice, IInternalDevice where TConfiguration : DeviceConfiguration
    {
        IInternalDeviceHost? _host;
        DeviceStatus _status;
        readonly PerfectEventSender<DeviceLifetimeEvent> _lifetimeChanged;
        readonly ActivityMonitor _commandMonitor;
        readonly CancellationTokenSource _destroyed;
        volatile TConfiguration _externalConfiguration;
        TConfiguration _currentConfiguration;
        long _failedChangeTimerNextTick;

        // For safety, ConfigurationStatus is copied: we don't trust the ExternalConfiguration.
        // This is internal so that the DeviceHostDaemon can use
        internal DeviceConfigurationStatus _configStatus;
        string? _controllerKey;
        bool _controllerKeyFromConfiguration;
        volatile bool _isRunning;

        // DeviceHostDaemon access to the actual safe status.
        DeviceConfigurationStatus IInternalDevice.ConfigStatus => _configStatus;


        /// <inheritdoc />
        public PerfectEvent<DeviceLifetimeEvent> LifetimeEvent => _lifetimeChanged.PerfectEvent;

        /// <summary>
        /// This can be overridden only by ActiveDevice (this is not available to regular devices).
        /// </summary>
        /// <param name="monitor">The command loop monitor.</param>
        /// <param name="e">The lifetime event to raise.</param>
        private protected virtual Task SafeRaiseLifetimeEventAsync( IActivityMonitor monitor, DeviceLifetimeEvent e ) => _lifetimeChanged.SafeRaiseAsync( monitor, e );

        /// <summary>
        /// Factory information (opaque token) that exposes the device's initial configuration.
        /// </summary>
        public readonly struct CreateInfo
        {
            /// <summary>
            /// Gets the configuration.
            /// Never null and <see cref="DeviceConfiguration.CheckValid(IActivityMonitor)"/> is necessarily true.
            /// This configuration is a "safe clone", the external world has no access to it: a reference to it can be
            /// kept by the device and, even if this would be weird, may safely be altered by the device.
            /// </summary>
            public readonly TConfiguration Configuration;

            internal readonly IInternalDeviceHost Host;
            internal readonly TConfiguration ExternalConfig;

            internal CreateInfo( TConfiguration c, TConfiguration externalConfig, IInternalDeviceHost h )
            {
                Configuration = c;
                Host = h;
                ExternalConfig = externalConfig;
            }
        }

        /// <summary>
        /// Initializes a new device bound to a configuration.
        /// Concrete device must expose a constructor with the exact same signature: initial configuration is handled by
        /// this constructor, warnings or errors must be logged and exception can be thrown if anything goes wrong.
        /// <para>
        /// The monitor here must be used only during the construction of the device. No reference to it must be kept.
        /// </para>
        /// </summary>
        /// <param name="monitor">
        /// The monitor to use for the initialization phase. A reference to this monitor must not be kept.
        /// </param>
        /// <param name="info">
        /// Contains the initial configuration to use. It must be <see cref="DeviceConfiguration.CheckValid"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </param>
        protected Device( IActivityMonitor monitor, CreateInfo info )
        {
            Throw.CheckNotNullArgument( monitor );
            TConfiguration config = info.Configuration;
            Debug.Assert( config != null && config.CheckValid( monitor ), "config != null && config.CheckValid( monitor )" );

            _host = info.Host;
            Name = config.Name;
            FullName = info.Host.DeviceHostName + '/' + Name;
            SystemDeviceFolderPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
            SystemDeviceFolderPath = SystemDeviceFolderPath.Combine( "CK/DeviceModel/" + FullName );

            _currentConfiguration = config;
            _externalConfiguration = info.ExternalConfig;
            _configStatus = config.Status;
            _controllerKey = String.IsNullOrEmpty( config.ControllerKey ) ? null : config.ControllerKey;
            _controllerKeyFromConfiguration = _controllerKey != null;
            _lifetimeChanged = new PerfectEventSender<DeviceLifetimeEvent>();

            _commandMonitor = new ActivityMonitor( $"Command loop for device {FullName}." );
            _commandMonitor.AutoTags = IDeviceHost.DeviceModel;
            _commandQueue = Channel.CreateUnbounded<BaseDeviceCommand>( new UnboundedChannelOptions() { SingleReader = true } );
            _commandQueueImmediate = Channel.CreateUnbounded<BaseDeviceCommand>( new UnboundedChannelOptions() { SingleReader = true } );
            _deferredCommands = new Queue<BaseDeviceCommand>();
            _destroyed = new CancellationTokenSource();
            _baseImmediateCommandLimit = info.Configuration.BaseImmediateCommandLimit;
            _immediateCommandLimitDirty = true;
            _ = Task.Run( CommandRunLoopAsync );
        }

        /// <summary>
        /// Gets the device name (relative to its host). Necessarily not null or whitespace.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the full name of this device: it is "<see cref="IDeviceHost.DeviceHostName"/>/<see cref="Name"/>".
        /// When <see cref="IsDestroyed"/> is true, " (Destroyed)" is added to it. 
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// Gets a fully rooted path in the application local folders that
        /// is "CK/DeviceModel/<see cref="IDeviceHost.DeviceHostName"/>/<see cref="Name"/>".
        /// <para>
        /// This path is independent of the current application: it is based on the device
        /// type (the host) and instance (this name).
        /// </para>
        /// <para>
        /// This folder is NOT created: it is up to the device, if it needs it, to call <see cref="System.IO.Directory.CreateDirectory(string)"/>
        /// (and up to it also to call the <see cref="System.IO.Directory.Delete(string)"/> from <see cref="DoDestroyAsync"/>).
        /// </para>
        /// </summary>
        public NormalizedPath SystemDeviceFolderPath { get; }

        /// <inheritdoc />
        public string? ControllerKey => _controllerKey;

        /// <summary>
        /// Gets whether this device has been started.
        /// From the implementation methods this property value is stable and can be trusted:
        /// <list type="bullet">
        ///     <item><see cref="DoReconfigureAsync"/>: true or false (this is the only method where it can be true or false).</item>
        ///     <item><see cref="DoStartAsync"/>: false.</item>
        ///     <item><see cref="DoStopAsync"/>: true.</item>
        ///     <item><see cref="DoDestroyAsync"/>: false (since it has been necessarily stopped before destroyed).</item>
        /// </list>
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets whether this device has been destroyed.
        /// </summary>
        public bool IsDestroyed => _destroyed.IsCancellationRequested;

        /// <summary>
        /// Gets a cancellation token that is bound to the destruction of this device.
        /// </summary>
        protected CancellationToken DestroyedToken => _destroyed.Token;

        /// <inheritdoc />
        public DeviceStatus Status => _status;

        DeviceConfiguration IDevice.ExternalConfiguration => _externalConfiguration;

        /// <summary>
        /// Gets a clone of the actual current configuration.
        /// This is NOT the actual configuration object reference that the device has received and
        /// is using: configuration objects are cloned in order to isolate the running device of any change
        /// in this publicly exposed configuration.
        /// <para>
        /// Even if changing this object is harmless, it should obviously not be changed.
        /// </para>
        /// </summary>
        public TConfiguration ExternalConfiguration => _externalConfiguration;

        /// <summary>
        /// Gets the current configuration. This is a clone of the last configuration submitted
        /// to <see cref="ReconfigureAsync(IActivityMonitor, TConfiguration, CancellationToken)"/> (or
        /// the <see cref="ConfigureDeviceCommand{THost, TConfiguration}"/>'s configuration command)
        /// that is accessible only from this device (protected).
        /// <para>
        /// </para>
        /// <para>
        /// Configuration are mutable but device's code should avoid to alter it.
        /// </para>
        /// From the implementation methods (<see cref="DoReconfigureAsync"/>, <see cref="DoStartAsync"/>,
        /// <see cref="DoStopAsync"/>, <see cref="DoDestroyAsync"/> and <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/>)
        /// this is stable and can be used freely.
        /// <para>
        /// <para>
        /// From other code (typically from external code in a <see cref="IActiveDevice"/>, this may change at any time: a reference
        /// to this current configuration should be captured once and reused as much as possible.
        /// </para>
        /// This is updated once <see cref="DoReconfigureAsync(IActivityMonitor, TConfiguration)"/> returned
        /// a successful result (<see cref="DeviceReconfiguredResult.UpdateSucceeded"/>).
        /// </para>
        /// </summary>
        protected TConfiguration CurrentConfiguration => _currentConfiguration;

        Task SetDeviceStatusAsync( DeviceStatus status )
        {
            if( _status != status )
            {
                _status = status;
                return SafeRaiseLifetimeEventAsync( _commandMonitor, new DeviceStatusChangedEvent( this, status ) );
            }
            return Task.CompletedTask;
        }

        #region Reconfigure
        Task<DeviceApplyConfigurationResult> IDevice.ReconfigureAsync( IActivityMonitor monitor, DeviceConfiguration configuration, CancellationToken token )
        {
            return ReconfigureAsync( monitor, (TConfiguration)configuration, token );
        }

        /// <summary>
        /// Applies a new configuration to this device.
        /// The configuration will be cloned and isolated from the external world.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration object.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The configuration result.</returns>
        public async Task<DeviceApplyConfigurationResult> ReconfigureAsync( IActivityMonitor monitor, TConfiguration configuration, CancellationToken token = default )
        {
            if( configuration == null ) throw new ArgumentNullException( nameof( configuration ) );
            if( !configuration.CheckValid( monitor ) )
            {
                return DeviceApplyConfigurationResult.InvalidConfiguration;
            }
            return await InternalReconfigureAsync( monitor, configuration, configuration.DeepClone(), token ).ConfigureAwait( false );
        }

        internal async Task<DeviceApplyConfigurationResult> InternalReconfigureAsync( IActivityMonitor monitor,
                                                                                      TConfiguration config,
                                                                                      TConfiguration clonedconfig,
                                                                                      CancellationToken token )
        {
            var cmd = (BaseConfigureDeviceCommand<TConfiguration>?)_host?.CreateLockedConfigureCommand( Name, _controllerKey, config, clonedconfig );
            if( cmd == null || !UnsafeSendCommand( monitor, cmd, token ) )
            {
                return DeviceApplyConfigurationResult.DeviceDestroyed;
            }
            return await cmd.Completion.Task.ConfigureAwait( false );
        }

        async Task HandleReconfigureAsync( BaseConfigureDeviceCommand<TConfiguration> cmd )
        {
            Debug.Assert( cmd.ClonedConfig != null );
            TConfiguration config = cmd.ClonedConfig;
            Debug.Assert( _host != null );

            // Configuration's ControllerKey and Status are applied even if the DoReconfigureAsync fails.
            // When the configuration's ControllerKey changes we could apply it only after a successful DoReconfigureAsync.
            // But for the Status it's not so easy: when Disabled, the device must be stopped before calling DoReconfigureAsync.
            // If DoReconfigureAsync fails, the configuration has de facto been applied (restarting it is certainly a bad idea).
            // One solution could be, when Disabled, to skip the DoReconfigureAsync (waiting for the next configuration) but
            // a device MAY depend on some of its configuration when in stopped state...
            // It's easier to consider that ControllerKey and Status are always applied and, in the rare case of a failing DoReconfigureAsync,
            // we update the current configurations with these updated fields.

            // Always applying BaseImmediateCommandLimit is easier to understand (as its documentation describes it).
            // We use the same pattern as for ControllerKey and Status.
            bool baseImmediateCommandLimitChanged = _baseImmediateCommandLimit != config.BaseImmediateCommandLimit;
            if( baseImmediateCommandLimitChanged )
            {
                _baseImmediateCommandLimit = config.BaseImmediateCommandLimit;
                _immediateCommandLimitDirty = true;
            }
            
            bool configStatusChanged = _configStatus != config.Status;
            bool stopDone = false;
            if( configStatusChanged )
            {
                // Handles ConfigStatus Disabled while we are running: stops the device (there can be no error here).
                if( _isRunning && config.Status == DeviceConfigurationStatus.Disabled )
                {
                    // The _configStatus is set to DeviceConfigurationStatus.Disabled by HostStopAsync that also 
                    // raised the StatusChanged: if nothing else has changed, we have no more event to raise.
                    // However we want the returned DeviceApplyConfigurationResult to the caller to not be "None"!
                    await HandleStopAsync( null, DeviceStoppedReason.StoppedByDisabledConfiguration ).ConfigureAwait( false );
                    Debug.Assert( _isRunning == false, "DoStop DOES stop." );
                    stopDone = true;
                }
                else
                {
                    _configStatus = config.Status;
                }
            }

            bool controllerKeyChanged = false;
            #region Handles a change of the configured ControllerKey (there can be no error here).
            var configKey = String.IsNullOrEmpty( config.ControllerKey ) ? null : config.ControllerKey;
            if( configKey == null && _controllerKeyFromConfiguration )
            {
                // Reset of the ControllerKey (previously from the configuration).
                _controllerKeyFromConfiguration = false;
                controllerKeyChanged = true;
            }
            else if( configKey != null )
            {
                // There is a configured ControllerKey.
                _controllerKeyFromConfiguration = true;
                controllerKeyChanged = configKey != _controllerKey;
            }
            if( controllerKeyChanged )
            {
                _commandMonitor.Info( $"Device {FullName}: controller key fixed by Configuration from '{_controllerKey}' to '{configKey}'." );
                _controllerKey = configKey;
            }
            #endregion
            // Calls DoReconfigureAsync. It may fail.
            // The error is captured and the completion is resolved at the end of process.
            Exception? error = null;
            DeviceReconfiguredResult reconfigResult;
            try
            {
                reconfigResult = await DoReconfigureAsync( _commandMonitor, config ).ConfigureAwait( false );
            }
            catch( Exception ex )
            {
                error = ex;
                _commandMonitor.Error( ex );
                reconfigResult = DeviceReconfiguredResult.UpdateFailed;
            }
            // If the device's own configuration has no change but configuration Status or ControllerKey changed
            // then the whole configuration has (successfully) changed.
            if( reconfigResult == DeviceReconfiguredResult.None && (configStatusChanged || controllerKeyChanged || baseImmediateCommandLimitChanged) )
            {
                reconfigResult = DeviceReconfiguredResult.UpdateSucceeded;
            }
            bool configActuallyChanged = reconfigResult == DeviceReconfiguredResult.UpdateSucceeded;

            // Now handles the edge case: the configuration Status, ControllerKey or BaseImmediateCommandLimit changed but
            // DoReconfigureAsync failed: we reuse the _currentConfiguration.  
            if( (reconfigResult == DeviceReconfiguredResult.UpdateFailed || reconfigResult == DeviceReconfiguredResult.UpdateFailedRestartRequired)
                && (configStatusChanged || controllerKeyChanged || baseImmediateCommandLimitChanged) )
            {
                Debug.Assert( !configActuallyChanged );
                config = _currentConfiguration;
                configActuallyChanged = true;
            }

            if( controllerKeyChanged ) config.ControllerKey = _controllerKey;
            if( configStatusChanged ) config.Status = _configStatus;
            if( baseImmediateCommandLimitChanged ) config.BaseImmediateCommandLimit = _baseImmediateCommandLimit;

            // Updates the configuration objects if changed.
            if( configActuallyChanged )
            {
                _currentConfiguration = config;
                _externalConfiguration = config.DeepClone();
            }

            // On success, or if nothing has been done, check for AlwaysRunning and tries to start the device if it is stopped.
            bool startDone = false;
            DeviceApplyConfigurationResult applyResult = (DeviceApplyConfigurationResult)reconfigResult;
            if( (reconfigResult == DeviceReconfiguredResult.UpdateSucceeded || reconfigResult == DeviceReconfiguredResult.None)
                && !_isRunning
                && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
            {
                await HandleStartAsync( null, DeviceStartedReason.StartedByAlwaysRunningConfiguration ).ConfigureAwait( false );
                if( !_isRunning )
                {
                    applyResult = DeviceApplyConfigurationResult.UpdateSucceededButStartFailed;
                }
                else
                {
                    // A StatusChanged has been emitted by HostStartAsync.
                    startDone = true;
                }
            }
            // If a start or a stop has been done:
            //  - the DeviceStatus has been updated (we skip the reconfigured result if it was a start: it's the last step).
            //  - if nothing happened, it's a success (we correct the result for the caller).
            if( startDone || stopDone )
            {
                Debug.Assert( startDone != stopDone, "Either we stopped (Disable) or started (AlwaysRunning)." );
                if( applyResult == DeviceApplyConfigurationResult.None )
                {
                    applyResult = DeviceApplyConfigurationResult.UpdateSucceeded;
                }
                // If we have initially stopped, the status has been updated.
                // But if we failed to apply the configuration, we override the status.
                if( stopDone
                    && reconfigResult != DeviceReconfiguredResult.UpdateSucceeded
                    && reconfigResult != DeviceReconfiguredResult.None )
                {
                    await SetDeviceStatusAsync( new DeviceStatus( reconfigResult, _isRunning, _host.DaemonStoppedToken.IsCancellationRequested ) ).ConfigureAwait( false );
                }
            }
            else
            {
                if( reconfigResult != DeviceReconfiguredResult.None )
                {
                    await SetDeviceStatusAsync( new DeviceStatus( reconfigResult, _isRunning, _host.DaemonStoppedToken.IsCancellationRequested ) ).ConfigureAwait( false );
                }
            }
            if( controllerKeyChanged )
            {
                await SafeRaiseLifetimeEventAsync( _commandMonitor, new DeviceControllerKeyChangedEvent( this, _controllerKey ) );
            }
            if( configActuallyChanged )
            {
                await SafeRaiseLifetimeEventAsync( _commandMonitor, new DeviceConfigurationChangedEvent( this, _externalConfiguration ) );
            }
            // Sets the completion last.
            // We use TrySet to handle any cancellation but this should be weird: the cancellation occurred in the middle
            // of the reconfiguration :(.
            bool complete = error != null ? cmd.Completion.TrySetException( error ) : cmd.Completion.TrySetResult( applyResult );
            if( !complete )
            {
                _commandMonitor.Warn( $"Reconfigure command has been completed outside of the normal process." );
            }
        }
        #endregion

        /// <summary>
        /// Reconfigures this device. This can be called when this device is started (<see cref="IsRunning"/> can be true) and
        /// if reconfiguration while running is not possible or supported, <see cref="DeviceReconfiguredResult.UpdateFailedRestartRequired"/>
        /// should be returned.
        /// <para>
        /// It is perfectly valid for this method to return <see cref="DeviceReconfiguredResult.None"/> if nothing happened instead of
        /// <see cref="DeviceReconfiguredResult.UpdateSucceeded"/>. When None is returned, we may avoid a useless raise of the
        /// <see cref="DeviceConfigurationChangedEvent"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The configuration to apply.</param>
        /// <returns>The reconfiguration result.</returns>
        protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );

        #region SetControllerKey
        /// <inheritdoc />
        public Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? key ) => SetControllerKeyAsync( monitor, false, null, key );

        /// <inheritdoc />
        public Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? current, string? key ) => SetControllerKeyAsync( monitor, true, current, key );

        async Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, bool checkCurrent, string? current, string? key )
        {
            var cmd = _host?.CreateSetControllerKeyDeviceCommand( Name, current, key );
            if( cmd == null || !SendCommand( monitor, cmd, false, checkCurrent, default ) )
            {
                return false;
            }
            return await cmd.Completion.Task.ConfigureAwait( false );
        }

        async Task HandleSetControllerKeyAsync( BaseSetControllerKeyDeviceCommand cmd )
        {
            var key = cmd.NewControllerKey;
            if( String.IsNullOrEmpty( key ) ) key = null;
            if( key != _controllerKey )
            {
                if( _controllerKeyFromConfiguration )
                {
                    _commandMonitor.Warn( $"Unable to take control of device '{FullName}' with key '{key}': key from configuration is '{_controllerKey}'." );
                    cmd.Completion.SetResult( false );
                    return;
                }
                _commandMonitor.Trace( $"Device {FullName}: controller key changed from '{_controllerKey}' to '{key}'." );
                _controllerKey = key;
                await SafeRaiseLifetimeEventAsync( _commandMonitor, new DeviceControllerKeyChangedEvent( this, key ) ).ConfigureAwait( false );
            }
            if( !cmd.Completion.TrySetResult( true ) )
            {
                _commandMonitor.Warn( $"Command has been completed outside of the normal process. This has been ignored." );
            }
        }

        #endregion

        #region Start

        /// <inheritdoc />
        public async Task<bool> StartAsync( IActivityMonitor monitor )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                await HandleStartAsync( null, DeviceStartedReason.SelfStart ).ConfigureAwait( false );
                return _isRunning;
            }
            var preCheck = SyncStateStartCheck( monitor );
            if( preCheck.HasValue )
            {
                return preCheck.Value;
            }
            var cmd = _host?.CreateStartCommand( Name );
            if( cmd == null || !UnsafeSendCommand( monitor, cmd ) )
            {
                monitor.Error( $"Starting a destroyed device '{FullName}' is not possible." );
                return false;
            }
            return await cmd.Completion.Task.ConfigureAwait( false );
        }

        bool? SyncStateStartCheck( IActivityMonitor monitor )
        {
            if( _configStatus == DeviceConfigurationStatus.Disabled )
            {
                monitor.Error( $"Device {FullName} is Disabled by configuration." );
                return false;
            }
            if( _isRunning )
            {
                monitor.Debug( "Already running." );
                return true;
            }
            return null;
        }

        async Task HandleStartAsync( BaseStartDeviceCommand? cmd, DeviceStartedReason reason )
        {
            Debug.Assert( _host != null );

            using( _commandMonitor.OpenInfo( $"Starting {FullName} ({reason})" ).ConcludeWith( () => _isRunning ? "Success." : "Failed." ) )
            {
                var check = SyncStateStartCheck( _commandMonitor );
                if( check.HasValue )
                {
                    cmd?.Completion.SetResult( check.Value );
                    return;
                }
                Exception? error = null;
                Debug.Assert( _isRunning == false );
                try
                {
                    if( await DoStartAsync( _commandMonitor, reason ).ConfigureAwait( false ) )
                    {
                        _isRunning = true;
                    }
                }
                catch( Exception ex )
                {
                    error = ex;
                    _commandMonitor.Error( $"While starting '{FullName}'.", ex );
                }
                if( _isRunning && reason != DeviceStartedReason.SilentAutoStartAndStopStoppedBehavior )
                {
                    await SetDeviceStatusAsync( new DeviceStatus( reason, _host.DaemonStoppedToken.IsCancellationRequested ) ).ConfigureAwait( false );
                }
                if( _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                {
                    _host.OnAlwaysRunningCheck( this, _commandMonitor );
                }
                // Sets the completion last.
                if( cmd != null )
                {
                    bool complete = error != null ? cmd.Completion.TrySetException( error ) : cmd.Completion.TrySetResult( _isRunning );
                    if( !complete )
                    {
                        _commandMonitor.Warn( $"Command has been completed outside of the normal process." );
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Implements this device's Start behavior.
        /// False must be returned if anything prevents this device to start (this can also throw).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reason">Reason of the start.</param>
        /// <returns>True if the device can successfully start, false otherwise.</returns>
        protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );

        #region Stop

        /// <inheritdoc />
        public async Task<bool> StopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                await HandleStopAsync( null, ignoreAlwaysRunning ? DeviceStoppedReason.SelfStoppedForceCall : DeviceStoppedReason.SelfStoppedCall ).ConfigureAwait( false );
                return !_isRunning;
            }
            var r = SyncStateStopCheck( monitor, ignoreAlwaysRunning );
            if( r.HasValue )
            {
                return r.Value;
            }
            var cmd = _host?.CreateStopCommand( Name, ignoreAlwaysRunning );
            if( cmd == null || !UnsafeSendCommand( monitor, cmd ) )
            {
                monitor.Warn( $"Stopping an already destroyed device '{FullName}'." );
                return true;
            }
            return await cmd.Completion.Task.ConfigureAwait( false );
        }

        bool? SyncStateStopCheck( IActivityMonitor monitor, bool ignoreAlwaysRunnig )
        {
            if( !_isRunning )
            {
                monitor.Warn( $"Stopping an already stopped device '{FullName}'." );
                return true;
            }
            if( !ignoreAlwaysRunnig && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
            {
                monitor.Error( $"Cannot stop device '{FullName}' because Status is AlwaysRunning." );
                return false;
            }
            return null;
        }

        internal async Task HandleStopAsync( BaseStopDeviceCommand? cmd, DeviceStoppedReason reason )
        {
            Debug.Assert( _host != null );
            var isAlwaysRunning = _configStatus == DeviceConfigurationStatus.AlwaysRunning;
            using( _commandMonitor.OpenInfo( $"Stopping {FullName} ({reason})" ) )
            {
                if( reason == DeviceStoppedReason.StoppedByDisabledConfiguration || reason == DeviceStoppedReason.Destroyed )
                {
                    _configStatus = DeviceConfigurationStatus.Disabled;
                }
                // StoppedForceCall, SelfStoppedForceCall, Destroyed and SelfDestroyed skips AlwaysRunning check.
                var r = SyncStateStopCheck( _commandMonitor, cmd?.IgnoreAlwaysRunning
                                                             ?? reason == DeviceStoppedReason.StoppedForceCall
                                                                || reason == DeviceStoppedReason.SelfStoppedForceCall
                                                                || reason == DeviceStoppedReason.Destroyed
                                                                || reason == DeviceStoppedReason.SelfDestroyed );
                Exception? error = null;
                if( !r.HasValue )
                {
                    // From now on, Stop always succeeds, even if an error occurred.
                    _isRunning = false;
                    r = true;
                    try
                    {
                        await DoStopAsync( _commandMonitor, reason ).ConfigureAwait( false );
                    }
                    catch( Exception ex )
                    {
                        error = ex;
                        _commandMonitor.Error( $"While stopping {FullName} ({reason}).", ex );
                    }
                    if( reason != DeviceStoppedReason.Destroyed
                        && reason != DeviceStoppedReason.SelfDestroyed
                        && reason != DeviceStoppedReason.SilentAutoStartAndStopStoppedBehavior )
                    {
                        await SetDeviceStatusAsync( new DeviceStatus( reason, _host.DaemonStoppedToken.IsCancellationRequested ) ).ConfigureAwait( false );
                    }
                    if( isAlwaysRunning )
                    {
                        _host.OnAlwaysRunningCheck( this, _commandMonitor );
                    }
                }
                // Sets Completion last.
                if( cmd != null )
                {
                    bool complete = error != null ? cmd.Completion.TrySetException( error ) : cmd.Completion.TrySetResult( r.Value );
                    if( !complete )
                    {
                        _commandMonitor.Warn( $"Command has been completed outside of the normal process." );
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Implements this device's Stop behavior.
        /// This should always succeed: after having called this method (that may throw), this device is considered stopped.
        /// Note that this method is never called if this device must be <see cref="DeviceConfigurationStatus.AlwaysRunning"/>
        /// (except with the <see cref="DeviceStoppedReason.Destroyed"/>) or if it is already stopped.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reason">The reason to stop.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );

        #region Destroy

        /// <inheritdoc />
        public async Task DestroyAsync( IActivityMonitor monitor, bool waitForDeviceDestroyed = true )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                await HandleDestroyAsync( null, true );
            }
            else
            {
                var cmd = _host?.CreateDestroyCommand( Name );
                if( cmd == null || !UnsafeSendCommand( monitor, cmd ) )
                {
                    monitor.Info( $"Destroying an already destroyed device '{FullName}'." );
                }
                else
                {
                    if( waitForDeviceDestroyed ) await cmd.Completion.Task.ConfigureAwait( false );
                }
            }
        }

        async Task HandleDestroyAsync( BaseDestroyDeviceCommand? cmd, bool autoDestroy )
        {
            Debug.Assert( _host != null );
            if( _isRunning )
            {
                await HandleStopAsync( null, autoDestroy ? DeviceStoppedReason.SelfDestroyed : DeviceStoppedReason.Destroyed ).ConfigureAwait( false );
                Debug.Assert( !_isRunning );
            }
            Exception? error = null;
            try
            {
                await DoDestroyAsync( _commandMonitor ).ConfigureAwait( false );
            }
            catch( Exception ex )
            {
                error = ex;
                _commandMonitor.Warn( $"'{FullName}'.OnDestroyAsync error. This is ignored.", ex );
            }
            FullName += " (Destroyed)";
            var h = _host;
            _host = null;
            _destroyed.Cancel();
            if( h.OnDeviceDoDestroy( _commandMonitor, this ) )
            {
                await h.RaiseDevicesChangedEventAsync( _commandMonitor ).ConfigureAwait( false );
            }
            await SetDeviceStatusAsync( new DeviceStatus( autoDestroy ? DeviceStoppedReason.SelfDestroyed : DeviceStoppedReason.Destroyed, h.DaemonStoppedToken.IsCancellationRequested ) ).ConfigureAwait( false );
            _lifetimeChanged.RemoveAll();
            await h.OnDeviceDestroyedAsync( _commandMonitor, this ).ConfigureAwait( false );
            if( cmd != null )
            {
                bool complete = error != null ? cmd.Completion.TrySetException( error ) : cmd.Completion.TrySetResult();
                if( !complete )
                {
                    _commandMonitor.Warn( $"Command has been completed outside of the normal process." );
                }
            }
        }

        #endregion

        /// <summary>
        /// Implements this device's destruction behavior.
        /// Specializations that expose events should call the <c>RemoveAll()</c> methods on all the exposed events.
        /// <para>
        /// Note that it is not possible to cancel/reject the destruction of the device: as long as it has no more configuration,
        /// or if <see cref="DestroyAsync"/> is called, a device is necessarily stopped and destroyed.
        /// </para>
        /// <para>
        /// Any exception raised by this method will be logged as a warning.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task DoDestroyAsync( IActivityMonitor monitor );

        /// <summary>
        /// Overridden to return the <see cref="FullName"/>.
        /// </summary>
        /// <returns>This device's FullName.</returns>
        public override string ToString() => FullName;
    }

}
