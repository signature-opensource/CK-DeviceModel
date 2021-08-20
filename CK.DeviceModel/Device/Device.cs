using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;
using CK.Text;
using System.Threading.Channels;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for a device.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract partial class Device<TConfiguration> : IDevice where TConfiguration : DeviceConfiguration
    {
        IInternalDeviceHost? _host;
        DeviceConfigurationStatus _configStatus;
        string? _controllerKey;
        DeviceStatus _status;
        readonly PerfectEventSender<DeviceLifetimeEvent> _lifetimeChanged;
        readonly ActivityMonitor _commandMonitor;
        readonly CancellationTokenSource _destroyed;
        TConfiguration _currentConfiguration;
        bool _controllerKeyFromConfiguration;
        volatile bool _isRunning;

        /// <inheritdoc />
        public PerfectEvent<DeviceLifetimeEvent> LifetimeEvent => _lifetimeChanged.PerfectEvent;

        /// <summary>
        /// This can be overridden only by ActiveDevice (this is not available to regular devices).
        /// </summary>
        private protected virtual Task SafeRaiseLifetimeEventAsync( DeviceLifetimeEvent e ) => _lifetimeChanged.SafeRaiseAsync( _commandMonitor, e );

        /// <summary>
        /// Factory information (opaque token).
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

            internal CreateInfo( TConfiguration c, IInternalDeviceHost h )
            {
                Configuration = c;
                Host = h;
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
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            TConfiguration config = info.Configuration;
            Debug.Assert( config != null && config.CheckValid( monitor ), "config != null && config.CheckValid( monitor )" );

            _host = info.Host;
            Name = config.Name;
            FullName = info.Host.DeviceHostName + '/' + Name;
            SystemDeviceFolderPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
            SystemDeviceFolderPath = SystemDeviceFolderPath.Combine( "CK/DeviceModel/" + FullName );

            _currentConfiguration = config;
            _configStatus = config.Status;
            _controllerKey = String.IsNullOrEmpty( config.ControllerKey ) ? null : config.ControllerKey;
            _controllerKeyFromConfiguration = _controllerKey != null;
            _lifetimeChanged = new PerfectEventSender<DeviceLifetimeEvent>();

            _commandMonitor = new ActivityMonitor( $"Command loop for device {FullName}." );
            _commandQueue = Channel.CreateUnbounded<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)>( new UnboundedChannelOptions() { SingleReader = true } );
            _commandQueueImmediate = Channel.CreateUnbounded<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)>( new UnboundedChannelOptions() { SingleReader = true } );
            _deferredCommands = new Queue<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)>();
            _destroyed = new CancellationTokenSource();
            _ = Task.Run( CommandRunLoop );
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

        /// <summary>
        /// Gets the current configuration. This is a clone of the last configuration submitted
        /// to <see cref="ReconfigureAsync(IActivityMonitor, TConfiguration, CancellationToken)"/> (or
        /// the <see cref="ConfigureDeviceCommand{THost, TConfiguration}"/>'s configuration command)
        /// that is accessible only (protected) from this device.
        /// <para>
        /// Configuration are mutable but device's code should avoid to alter it.
        /// </para>
        /// <para>
        /// This is updated once <see cref="DoReconfigureAsync(IActivityMonitor, TConfiguration)"/> returned
        /// a successful result (<see cref="DeviceReconfiguredResult.UpdateSucceeded"/>).
        /// </para>
        /// </summary>
        protected TConfiguration CurrentConfiguration => _currentConfiguration;

        Task SetDeviceStatusAsync( DeviceStatus status )
        {
            _status = status;
            return SafeRaiseLifetimeEventAsync( new DeviceStatusChangedEvent( this, status ) );
        }

        /// <summary>
        /// Gets the current configuration status of this device.
        /// From the implementation methods (<see cref="DoReconfigureAsync"/>, <see cref="DoStartAsync"/>,
        /// <see cref="DoStopAsync"/>, <see cref="DoDestroyAsync"/> and <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand, CancellationToken)"/>)
        /// this property value is stable and can be trusted.
        /// </summary>
        public DeviceConfigurationStatus ConfigurationStatus => _configStatus;

        #region Reconfigure

        /// <summary>
        /// Applies a new configuration to this device.
        /// The configuration will be cloned and isolated from the external world.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The configuration object.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The configuration result.</returns>
        public Task<DeviceApplyConfigurationResult> ReconfigureAsync( IActivityMonitor monitor, TConfiguration config, CancellationToken token = default )
        {
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            if( !config.CheckValid( monitor ) )
            {
                return Task.FromResult( DeviceApplyConfigurationResult.InvalidConfiguration );
            }
            return InternalReconfigureAsync( monitor, config, config.DeepClone(), token );
        }

        internal Task<DeviceApplyConfigurationResult> InternalReconfigureAsync( IActivityMonitor monitor,
                                                                                TConfiguration externalConfig,
                                                                                TConfiguration validAndClonedconfig,
                                                                                CancellationToken token = default )
        {
            var cmd = (BaseConfigureDeviceCommand<TConfiguration>?)_host?.CreateConfigureCommand( Name, validAndClonedconfig );
            if( cmd == null )
            {
                return Task.FromResult( DeviceApplyConfigurationResult.DeviceDestroyed );
            }
            cmd.ExternalConfig = externalConfig;
            if( !UnsafeSendCommandImmediate( monitor, cmd ) )
            {
                return Task.FromResult( DeviceApplyConfigurationResult.DeviceDestroyed );
            }
            return cmd.Completion.Task;
        }

        async Task HandleReconfigureAsync( BaseConfigureDeviceCommand<TConfiguration> cmd, CancellationToken token )
        {
            Debug.Assert( cmd.Configuration != null );
            TConfiguration config;
            if( cmd.ExternalConfig == null )
            {
                cmd.ExternalConfig = cmd.Configuration;
                config = cmd.Configuration.DeepClone();
            }
            else
            {
                config = cmd.Configuration;
            }

            Debug.Assert( _host != null );

            bool specialCaseOfDisabled = false;
            bool configStatusChanged = _configStatus != config.Status;
            if( _isRunning && config.Status == DeviceConfigurationStatus.Disabled )
            {
                // The _configStatus is set to DeviceConfigurationStatus.Disabled by HostStopAsync that also 
                // raised the StatusChanged: if nothing else has changed, we have no more event to raise.
                // However we want the returned DeviceApplyConfigurationResult to the caller to not be "None"!
                // This is why this awful specialCaseOfDisabled is here: to ultimately correct the returned result.
                await HandleStopAsync( null, DeviceStoppedReason.StoppedByDisabledConfiguration ).ConfigureAwait( false );
                Debug.Assert( _isRunning == false, "DoStop DOES stop." );
                configStatusChanged = false;
                specialCaseOfDisabled = true;
            }
            else
            {
                _configStatus = config.Status;
            }
            var configKey = String.IsNullOrEmpty( config.ControllerKey ) ? null : config.ControllerKey;
            _controllerKeyFromConfiguration = configKey != null;
            bool controllerKeyChanged = _controllerKeyFromConfiguration && configKey != _controllerKey;
            if( controllerKeyChanged )
            {
                _commandMonitor.Info( $"Device {FullName}: controller key fixed by Configuration from '{_controllerKey}' to '{configKey}'." );
                _controllerKey = configKey;
            }
            DeviceReconfiguredResult reconfigResult;
            try
            {
                reconfigResult = await DoReconfigureAsync( _commandMonitor, config ).ConfigureAwait( false );
                if( reconfigResult == DeviceReconfiguredResult.None && (configStatusChanged || controllerKeyChanged) )
                {
                    reconfigResult = DeviceReconfiguredResult.UpdateSucceeded;
                }
            }
            catch( Exception ex )
            {
                _commandMonitor.Error( ex );
                reconfigResult = DeviceReconfiguredResult.UpdateFailed;
            }

            bool configurationChanged = false;
            bool shouldEmitDeviceStatusChanged = reconfigResult != DeviceReconfiguredResult.None;
            if( shouldEmitDeviceStatusChanged )
            {
                // Don't emit the change of status right now.
                _status = new DeviceStatus( reconfigResult, _isRunning );
                if( reconfigResult == DeviceReconfiguredResult.UpdateSucceeded )
                {
                    _currentConfiguration = config;
                    configurationChanged = true;
                }
            }

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
                    shouldEmitDeviceStatusChanged = false;
                }
            }
            if( specialCaseOfDisabled && applyResult == DeviceApplyConfigurationResult.None )
            {
                applyResult = DeviceApplyConfigurationResult.UpdateSucceeded;
            }
            if( shouldEmitDeviceStatusChanged )
            {
                await SafeRaiseLifetimeEventAsync( new DeviceStatusChangedEvent( this, _status ) ).ConfigureAwait( false );
            }
            if( controllerKeyChanged )
            {
                await SafeRaiseLifetimeEventAsync( new DeviceControllerKeyChangedEvent( this, _controllerKey ) );
            }
            if( configurationChanged )
            {
                await SafeRaiseLifetimeEventAsync( new DeviceConfigurationChangedEvent( this, cmd.ExternalConfig ) );
            }
            if( applyResult != DeviceApplyConfigurationResult.None && _host.OnDeviceConfigured( _commandMonitor, this, applyResult, cmd.ExternalConfig ) )
            {
                await _host.RaiseDevicesChangedEvent( _commandMonitor ).ConfigureAwait( false );
            }
            cmd.Completion.SetResult( applyResult );
        }
        #endregion

        /// <summary>
        /// Reconfigures this device. This can be called when this device is started (<see cref="IsRunning"/> can be true) and
        /// if reconfiguration while running is not possible or supported, <see cref="DeviceReconfiguredResult.UpdateFailedRestartRequired"/>
        /// should be returned.
        /// <para>
        /// It is perfectly valid for this method to return <see cref="DeviceReconfiguredResult.None"/> if nothing happened instead of
        /// <see cref="DeviceReconfiguredResult.UpdateSucceeded"/>. When None is returned, we may avoid a useless update of the <see cref="IDeviceHost"/>
        /// set of configured devices.
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

        Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, bool checkCurrent, string? current, string? key )
        {
            var cmd = _host?.CreateSetControllerKeyDeviceCommand( Name, current, key );
            if( cmd == null || !SendCommandImmediate( monitor, cmd, false, checkCurrent, default ) )
            {
                return Task.FromResult( false );
            }
            return cmd.Completion.Task;
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
                await SafeRaiseLifetimeEventAsync( new DeviceControllerKeyChangedEvent( this, key ) ).ConfigureAwait( false );
            }
            cmd.Completion.SetResult( true );
        }

        #endregion

        #region Start

        /// <inheritdoc />
        public Task<bool> StartAsync( IActivityMonitor monitor )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                return HandleStartAsync( null, DeviceStartedReason.SelfStart ).ContinueWith( _ => _isRunning );
            }
            var preCheck = SyncStateStartCheck( monitor );
            if( preCheck.HasValue )
            {
                return Task.FromResult( preCheck.Value );
            }
            var cmd = _host?.CreateStartCommand( Name );
            if( cmd == null || !UnsafeSendCommandImmediate( monitor, cmd ) )
            {
                monitor.Error( $"Starting a destroyed device '{FullName}' is not possible." );
                return Task.FromResult( false );
            }
            return cmd.Completion.Task;
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
                    _commandMonitor.Error( $"While starting '{FullName}'.", ex );
                }
                if( _isRunning && reason != DeviceStartedReason.SilentAutoStartAndStopStoppedBehavior )
                {
                    await SetDeviceStatusAsync( new DeviceStatus( reason ) ).ConfigureAwait( false );
                }
                if( _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                {
                    _host.OnAlwaysRunningCheck( this, _commandMonitor );
                }
                cmd?.Completion.SetResult( _isRunning );
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
        public Task<bool> StopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                return HandleStopAsync( null, ignoreAlwaysRunning ? DeviceStoppedReason.SelfStoppedForceCall : DeviceStoppedReason.SelfStoppedCall )
                        .ContinueWith( t => !_isRunning );
            }
            var r = SyncStateStopCheck( monitor, ignoreAlwaysRunning );
            if( r.HasValue )
            {
                return Task.FromResult( r.Value );
            }
            var cmd = _host?.CreateStopCommand( Name, ignoreAlwaysRunning );
            if( cmd == null || !UnsafeSendCommandImmediate( monitor, cmd ) )
            {
                monitor.Warn( $"Stopping an already destroyed device '{FullName}'." );
                return Task.FromResult( true );
            }
            return cmd.Completion.Task;
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
                        _commandMonitor.Error( $"While stopping {FullName} ({reason}).", ex );
                    }
                    if( reason != DeviceStoppedReason.Destroyed
                        && reason != DeviceStoppedReason.SelfDestroyed
                        && reason != DeviceStoppedReason.SilentAutoStartAndStopStoppedBehavior )
                    {
                        await SetDeviceStatusAsync( new DeviceStatus( reason ) ).ConfigureAwait( false );
                    }
                    if( isAlwaysRunning )
                    {
                        _host.OnAlwaysRunningCheck( this, _commandMonitor );
                    }
                }
                cmd?.Completion.SetResult( r.Value );
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

        /// <summary>
        /// Destroys this device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        public Task DestroyAsync( IActivityMonitor monitor )
        {
            if( monitor.Output == _commandMonitor.Output )
            {
                return HandleDestroyAsync( null, true );
            }
            var cmd = _host?.CreateDestroyCommand( Name );
            if( cmd == null || !UnsafeSendCommandImmediate( monitor, cmd ) )
            {
                monitor.Info( $"Destroying an already destroyed device '{FullName}'." );
                return Task.CompletedTask;
            }
            return cmd.Completion.Task;
        }

        async Task HandleDestroyAsync( BaseDestroyDeviceCommand? cmd, bool autoDestroy )
        {
            Debug.Assert( _host != null );
            if( _isRunning )
            {
                await HandleStopAsync( null, autoDestroy ? DeviceStoppedReason.SelfDestroyed : DeviceStoppedReason.Destroyed ).ConfigureAwait( false );
                Debug.Assert( !_isRunning );
            }
            try
            {
                await DoDestroyAsync( _commandMonitor ).ConfigureAwait( false );
            }
            catch( Exception ex )
            {
                _commandMonitor.Warn( $"'{FullName}'.OnDestroyAsync error. This is ignored.", ex );
            }
            FullName += " (Destroyed)";
            var h = _host;
            _host = null;
            _destroyed.Cancel();
            if( h.OnDeviceDestroyed( _commandMonitor, this ) )
            {
                await h.RaiseDevicesChangedEvent( _commandMonitor ).ConfigureAwait( false );
            }
            await SetDeviceStatusAsync( new DeviceStatus( autoDestroy ? DeviceStoppedReason.SelfDestroyed : DeviceStoppedReason.Destroyed ) ).ConfigureAwait( false );
            cmd?.Completion.SetResult();
            _lifetimeChanged.RemoveAll();
        }

        #endregion

        /// <summary>
        /// Implements this device's destruction behavior.
        /// Specializations that expose events should call the <c>RemoveAll()</c> methods on all the exposed events.
        /// <para>
        /// Note that it is not possible to cancel/reject the destruction of the device: as long as it has no more configuration,
        /// or if <see cref="DestroyAsync(IActivityMonitor)"/> is called, a device is necessarily stopped and destroyed.
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
