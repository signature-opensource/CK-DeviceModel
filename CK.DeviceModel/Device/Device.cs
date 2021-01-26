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

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for a device.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract class Device<TConfiguration> : IDevice, IInternalDevice where TConfiguration : DeviceConfiguration
    {
        IInternalDeviceHost? _host;
        DeviceConfigurationStatus _configStatus;
        string? _controllerKey;
        DeviceStatus _status;
        bool _controllerKeyFromConfiguration;
        bool _isRunning;
        readonly PerfectEventSender<IDevice> _statusChanged;
        readonly PerfectEventSender<IDevice, string?> _controllerKeyChanged;

        /// <inheritdoc />
        public PerfectEvent<IDevice> StatusChanged => _statusChanged.PerfectEvent;

        /// <inheritdoc />
        public PerfectEvent<IDevice, string?> ControllerKeyChanged => _controllerKeyChanged.PerfectEvent;

        /// <summary>
        /// Initializes a new device bound to a configuration.
        /// Concrete device must expose a constructor with the exact same signature: initial configuration is handled by
        /// this constructor, warnings or errors must be logged and exception can be thrown if anything goes wrong. 
        /// </summary>
        /// <param name="monitor">
        /// The monitor to use for the initialization phase. A reference to this monitor must not be kept.
        /// </param>
        /// <param name="config">
        /// The initial configuration to use. It must be <see cref="DeviceConfiguration.CheckValid"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </param>
        protected Device( IActivityMonitor monitor, TConfiguration config )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            if( !config.CheckValid( monitor ) ) throw new ArgumentException( "Configuration must be valid.", nameof( config ) );

            _statusChanged = new PerfectEventSender<IDevice>();
            _controllerKeyChanged = new PerfectEventSender<IDevice, string?>();
            Name = config.Name;
            _configStatus = config.Status;
            _controllerKey = String.IsNullOrEmpty( config.ControllerKey ) ? null : config.ControllerKey;
            _controllerKeyFromConfiguration = _controllerKey != null;
            FullName = null!;
        }

        internal void HostSetHost( IInternalDeviceHost host )
        {
            Debug.Assert( _host == null );
            _host = host;
            FullName = host.DeviceHostName + '/' + Name;
            DeviceFolderPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
            DeviceFolderPath = DeviceFolderPath.Combine( "CK/DeviceModel" + FullName );
        }

        internal Task HostDestroyAsync( IActivityMonitor monitor )
        {
            Debug.Assert( _host != null && !_isRunning );
            _host = null;
            FullName += " (Detached)";
            return DoDestroyAsync( monitor );
        }

        internal async Task HostRaiseDestroyStatusAsync( IActivityMonitor monitor )
        {
            await SetDeviceStatusAsync( monitor, new DeviceStatus( DeviceStoppedReason.Destroyed ) );
            _statusChanged.RemoveAll();
            _controllerKeyChanged.RemoveAll();
        }

        /// <summary>
        /// Gets the name. Necessarily not null or whitespace.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the full name of this device: it is "<see cref="IDeviceHost.DeviceHostName"/>/<see cref="Name"/>".
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
        /// This folder is NOT created: it is up to the device, if it needs it to call <see cref="System.IO.Directory.CreateDirectory(string)"/>
        /// (and may handle the <see cref="System.IO.Directory.Delete(string)"/> from <see cref="DoDestroyAsync"/>).
        /// </para>
        /// </summary>
        public NormalizedPath DeviceFolderPath { get; private set; }

        /// <inheritdoc />
        public string? ControllerKey => _controllerKey;

        /// <summary>
        /// Sets a new <see cref="ControllerKey"/>, whatever its current value is.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="key">The controller key.</param>
        /// <returns>
        /// True if it has been changed, false otherwise, typically because the key has been fixed
        /// by the <see cref="DeviceConfiguration.ControllerKey"/>.
        /// </returns>
        public Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? key ) => SetControllerKeyAsync( monitor, false, null, key );

        /// <summary>
        /// Sets a new <see cref="ControllerKey"/> only if the current one is the same as the specified <paramref name="current"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="current">The current value to challenge.</param>
        /// <param name="key">The controller key to set.</param>
        /// <returns>
        /// True if it has been changed, false otherwise: either the current key doesn't match or the
        /// key has been fixed by the <see cref="DeviceConfiguration.ControllerKey"/>.
        /// </returns>
        public Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? current, string? key ) => SetControllerKeyAsync( monitor, true, current, key );

        Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, bool checkCurrent, string? current, string? key )
        {
            IInternalDeviceHost? h = _host;
            return h == null
                    ? Task.FromResult( SetControllerKeyOnDetachedAsync( monitor, key, this ) )
                    : h.SetControllerKeyAsync( this, monitor, checkCurrent, current, key );
        }

        internal static bool SetControllerKeyOnDetachedAsync( IActivityMonitor monitor, string? key, IDevice d )
        {
            monitor.Error( $"Setting controller key '{key}' on detached device '{d.FullName}' is not possible." );
            return false;
        }

        internal PerfectEventSender<IDevice, string?>? HostSetControllerKey( IActivityMonitor monitor, bool checkCurrent, string? current, string? key )
        {
            if( String.IsNullOrEmpty( key ) ) key = null;
            if( key != _controllerKey )
            {
                if( _controllerKeyFromConfiguration )
                {
                    monitor.Warn( $"Unable to take control of device '{FullName}' with key '{key}': key from configuration is '{_controllerKey}'." );
                    return null;
                }
                if( checkCurrent && _controllerKey != null && current != _controllerKey )
                {
                    monitor.Warn( $"Unable to take control of device '{FullName}' with key '{key}': expected key is '{current}' but current key is '{_controllerKey}'." );
                    return null;
                }
                monitor.Trace( $"Device {FullName}: controller key changed from '{_controllerKey}' to '{key}'." );
                _controllerKey = key;
            }
            return _controllerKeyChanged;
        }


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
        public bool IsDestroyed => _host == null;

        /// <inheritdoc />
        public DeviceStatus Status => _status;

        Task SetDeviceStatusAsync( IActivityMonitor monitor, DeviceStatus status )
        {
            _status = status;
            return _statusChanged.SafeRaiseAsync( monitor, this );
        }

        /// <summary>
        /// Gets the current configuration status of this device.
        /// From the implementation methods (<see cref="DoReconfigureAsync"/>, <see cref="DoStartAsync"/>,
        /// <see cref="DoStopAsync"/> and <see cref="DoDestroyAsync"/>) this property value is stable and can be trusted.
        /// </summary>
        public DeviceConfigurationStatus ConfigurationStatus => _configStatus;

        internal async Task<(DeviceApplyConfigurationResult, Func<Task>?)> HostReconfigureAsync( IActivityMonitor monitor, TConfiguration config )
        {
            Debug.Assert( config.Name == Name );

            bool specialCaseOfDisabled = false;
            bool configStatusChanged = _configStatus != config.Status;
            if( _isRunning && config.Status == DeviceConfigurationStatus.Disabled )
            {
                // The _configStatus is set to DeviceConfigurationStatus.Disabled by HostStopAsync that also 
                // raised the StatusChanged: if the nothing else has changed, we have no more event to raise.
                // However we want the returned DeviceApplyConfigurationResult to the caller to not be "None"!
                // This is why this awful specialCaseOfDisabled is here: to ultimately correct the returned result.
                await HostStopAsync( monitor, DeviceStoppedReason.StoppedByDisabledConfiguration );
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
                monitor.Info( $"Device {FullName}: controller key fixed by Configuration from '{_controllerKey}' to '{configKey}'." );
                _controllerKey = configKey;
            }
            DeviceReconfiguredResult reconfigResult;
            try
            {
                reconfigResult = await DoReconfigureAsync( monitor, config );
                if( reconfigResult == DeviceReconfiguredResult.None && (configStatusChanged || controllerKeyChanged) )
                {
                    reconfigResult = DeviceReconfiguredResult.UpdateSucceeded;
                }
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                reconfigResult = DeviceReconfiguredResult.UpdateFailed;
            }

            bool shouldEmitDeviceStatusChanged = reconfigResult != DeviceReconfiguredResult.None;
            if( shouldEmitDeviceStatusChanged )
            {
                // Don't emit the change of status right now.
                _status = new DeviceStatus( reconfigResult, _isRunning );
            }

            DeviceApplyConfigurationResult applyResult = (DeviceApplyConfigurationResult)reconfigResult;
            if( (reconfigResult == DeviceReconfiguredResult.UpdateSucceeded || reconfigResult == DeviceReconfiguredResult.None)
                && !_isRunning
                && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
            {
                if( !await HostStartAsync( monitor, DeviceStartedReason.StartedByAlwaysRunningConfiguration ) )
                {
                    Debug.Assert( !_isRunning );
                    applyResult = DeviceApplyConfigurationResult.UpdateSucceededButStartFailed;
                }
                else
                {
                    Debug.Assert( _isRunning );
                    // A StatusChanged has been emitted by HostStartAsync.
                    shouldEmitDeviceStatusChanged = false;
                }
            }
            if( shouldEmitDeviceStatusChanged )
            {
                await _statusChanged.SafeRaiseAsync( monitor, this );
            }
            if( specialCaseOfDisabled && applyResult == DeviceApplyConfigurationResult.None )
            {
                applyResult = DeviceApplyConfigurationResult.UpdateSucceeded;
            }
            if( controllerKeyChanged )
            {
                return (applyResult, () => _controllerKeyChanged.SafeRaiseAsync( monitor, this, _controllerKey ));
            }
            return (applyResult, null);
        }

        /// <summary>
        /// Reconfigures this device. This can be called when this device is started (<see cref="IsRunning"/> can be true) and
        /// if reconfiguration while running is not possible or supported, <see cref="DeviceReconfiguredResult.UpdateFailedRestartRequired"/>
        /// should be returned.
        /// <para>
        /// It is perfectly valid for this method to return <see cref="DeviceReconfiguredResult.None"/> if nothing happened instead of
        /// <see cref="DeviceReconfiguredResult.UpdateSucceeded"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The configuration to apply.</param>
        /// <returns>The reconfiguration result.</returns>
        protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );

        /// <inheritdoc />
        public Task<bool> StartAsync( IActivityMonitor monitor )
        {
            IInternalDeviceHost? h = _host;
            if( h == null )
            {
                monitor.Error( $"Starting a detached device '{FullName}' is not possible." );
                return Task.FromResult( false );
            }
            var preCheck = SyncStateStartCheck( monitor );
            if( preCheck.HasValue ) return Task.FromResult( preCheck.Value );
            return h.StartAsync( this, monitor );
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

        internal async Task<bool> HostStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            Debug.Assert( _host != null );
            using( monitor.OpenInfo( $"Starting {FullName} ({reason})" ).ConcludeWith( () => _isRunning ? "Success." : "Failed." ) )
            {
                var check = SyncStateStartCheck( monitor );
                if( check.HasValue ) return check.Value;
                Debug.Assert( _isRunning == false );
                try
                {
                    if( await DoStartAsync( monitor, reason ) )
                    {
                        _isRunning = true;
                    }
                }
                catch( Exception ex )
                {
                    monitor.Error( $"While starting '{FullName}'.", ex );
                }
                if( _isRunning ) await SetDeviceStatusAsync( monitor, new DeviceStatus( reason ) );
                if( _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                {
                    _host.OnAlwaysRunningCheck( this, monitor );
                }
                return _isRunning;
            }
        }

        /// <summary>
        /// Implements this device's Start behavior.
        /// False must be returned if anything prevents this device to start (this can throw).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reason">Reason of the start.</param>
        /// <returns>True if the device has been successfully started, false otherwise.</returns>
        protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );

        /// <inheritdoc />
        public Task<bool> StopAsync( IActivityMonitor monitor )
        {
            var h = _host;
            if( h == null )
            {
                monitor.Warn( $"Stopping an already detached device '{FullName}'." );
                return Task.FromResult( true );
            }
            if( !_isRunning )
            {
                monitor.Warn( $"Stopping an already stopped device '{FullName}'." );
                return Task.FromResult( true );
            }
            var preCheck = SyncStateStopCheck( monitor );
            if( preCheck.HasValue ) return Task.FromResult( preCheck.Value );
            return h.StopAsync( this, monitor );
        }

        bool? SyncStateStopCheck( IActivityMonitor monitor )
        {
            if( _configStatus == DeviceConfigurationStatus.AlwaysRunning )
            {
                monitor.Error( $"Cannot stop device '{FullName}' because Status is AlwaysRunning." );
                return false;
            }
            return null;
        }

        internal async Task<bool> HostStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            Debug.Assert( _host != null && _isRunning );
            var isAlwaysRunning = _configStatus == DeviceConfigurationStatus.AlwaysRunning;
            using( monitor.OpenInfo( $"Stopping {FullName} ({reason})" ) )
            {
                if( reason == DeviceStoppedReason.StoppedByDisabledConfiguration || reason == DeviceStoppedReason.Destroyed )
                {
                    _configStatus = DeviceConfigurationStatus.Disabled;
                }
                // AutoStoppedForceCall skips AlwaysRunning check.
                if( reason != DeviceStoppedReason.AutoStoppedForceCall )
                {
                    var check = SyncStateStopCheck( monitor );
                    if( check.HasValue ) return check.Value;
                }
                // From now on, Stop always succeeds, even if an error occurred.
                try
                {
                    await DoStopAsync( monitor, reason );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"While stopping {FullName} ({reason}).", ex );
                }
                finally
                {
                    _isRunning = false;
                }
                if( reason != DeviceStoppedReason.Destroyed )
                {
                    await SetDeviceStatusAsync( monitor, new DeviceStatus( reason ) );
                }
                if( isAlwaysRunning )
                {
                    _host.OnAlwaysRunningCheck( this, monitor );
                }
            }
            return true;
        }

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

        /// <summary>
        /// Implements this device's destruction behavior.
        /// Specializations that expose events should call the <c>RemoveAll()</c> methods on all the exposed events.
        /// <para>
        /// Note that it is not possible to cancel/reject the destruction of the device: as long as it has no more configuration,
        /// a device is necessarily stopped and destroyed.
        /// </para>
        /// <para>
        /// Any exception raised by this method will be logged as a warning.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task DoDestroyAsync( IActivityMonitor monitor );

        /// <summary>
        /// This method can be called at any time: this device is destroyed as if no more configuration
        /// for its <see cref="Name"/> appeared in a full reconfiguration.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        protected Task AutoDestroyAsync( IActivityMonitor monitor )
        {
            return _host?.AutoDestroyAsync( this, monitor ) ?? Task.CompletedTask;
        }

        /// <summary>
        /// This method can be called at any time to stop this device, optionally ignoring the <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// Note that the <see cref="ConfigurationStatus"/> is left unchanged: the state of the system is what it should be: a device
        /// that has been configured to be always running is actually stopped.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ignoreAlwaysRunning">True to stop even if <see cref="ConfigurationStatus"/> states that this device must always run.</param>
        /// <returns>Always true except if <paramref name="ignoreAlwaysRunning"/> is false and the configuration is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.</returns>
        protected Task<bool> AutoStopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false )
        {
            return _host?.AutoStopAsync( this, monitor, ignoreAlwaysRunning ) ?? Task.FromResult(true);
        }

        /// <summary>
        /// Supports direct execution of a device command instead of being routed by <see cref="IDeviceHost.Handle(IActivityMonitor, DeviceCommand)"/>.
        /// An <see cref="ArgumentException"/> is raised if:
        /// <list type="bullet">
        ///     <item><see cref="DeviceCommand.HostType"/> is not compatible with this device's host type;</item>
        ///     <item>or <see cref="DeviceCommand.CheckValidity(IActivityMonitor)"/> fails;</item>
        ///     <item>or the <see cref="DeviceCommand.DeviceName"/> doesn't match this device's name;</item>
        ///     <item>or this <see cref="ControllerKey"/> is not null and <see cref="DeviceCommand.ControllerKey"/> differs from it.</item>
        /// </list>
        /// The 2 last checks can be suppressed thanks to the <paramref name="checkDeviceName"/> and <paramref name="checkControllerKey"/> parameters.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute synchronously.</param>
        /// <param name="checkDeviceName">
        /// By default, the <see cref="DeviceCommand.DeviceName"/> must be this <see cref="Name"/> otherwise an <see cref="ArgumentException"/> is thrown.
        /// Using false here allows any command name to be executed.
        /// </param>
        /// <param name="checkControllerKey">
        /// By default, the <see cref="DeviceCommand.ControllerKey"/> must match this <see cref="ControllerKey"/> (when not null).
        /// Using false here skips this check.
        /// </param>
        public void ExecuteCommand( IActivityMonitor monitor, SyncDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName, checkControllerKey );
            DoHandleCommand( monitor, command );
        }

        /// <summary>
        /// Like <see cref="ExecuteCommand"/> except that <see cref="DeviceCommand.DeviceName"/> is not checked against <see cref="Name"/>
        /// and <see cref="DeviceCommand.ControllerKey"/> is not match against <see cref="ControllerKey"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute synchronously.</param>
        public void UnsafeExecuteCommand( IActivityMonitor monitor, SyncDeviceCommand command )
        {
            CheckDirectCommandParameter( monitor, command, false, false );
            DoHandleCommand( monitor, command );
        }

        /// <inheritdoc cref="ExecuteCommand"/>
        public Task ExecuteCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName, checkControllerKey );
            return ExecuteWithBasicCommandAsync( monitor, command );
        }

        void CheckDirectCommandParameter( IActivityMonitor monitor, DeviceCommand command, bool checkDeviceName, bool checkControllerKey )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( _host!.GetType() ) ) throw new ArgumentException( $"{command.GetType().Name}: Invalid HostType '{command.HostType.Name}'.", nameof( command ) );
            if( !command.CheckValidity( monitor ) ) throw new ArgumentException( $"{command.GetType().Name}: CheckValidity failed. See logs.", nameof( command ) );
            if( checkDeviceName && command.DeviceName != Name ) throw new ArgumentException( $"{command.GetType().Name}: Command DeviceName is '{command.DeviceName}', device '{Name}' cannot execute it. (For direct execution, you can use checkDeviceName: false parameter to skip this check.)", nameof( command ) );
            if( checkControllerKey )
            {
                var invalidKey = CheckCommandControllerKey( command );
                if( invalidKey != null ) throw new ArgumentException( $"{command.GetType().Name}: {invalidKey}" );
            }
        }

        internal string? CheckCommandControllerKey( DeviceCommand command )
        {
            // The basic ResetControllerKey command sets a new ControllerKey.
            if( !(command is BasicControlDeviceCommand b) || b.Operation != BasicControlDeviceOperation.ResetControllerKey )
            {
                var key = ControllerKey;
                if( key != null && command.ControllerKey != key )
                {
                    return $"Expected command ControllerKey is '{command.ControllerKey}' but current device's one is '{key}'. (For direct execution, you can use checkControllerKey: false parameter to skip this check.)";
                }
            }
            return null;
        }

        void IInternalDevice.Execute( IActivityMonitor monitor, SyncDeviceCommand c ) => DoHandleCommand( monitor, c );

        Task IInternalDevice.ExecuteAsync( IActivityMonitor monitor, AsyncDeviceCommand c ) => ExecuteWithBasicCommandAsync( monitor, c );

        Task ExecuteWithBasicCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand c )
        {
            if( c is BasicControlDeviceCommand b )
            {
                switch( b.Operation )
                {
                    case BasicControlDeviceOperation.Start: return StartAsync( monitor );
                    case BasicControlDeviceOperation.Stop: return StopAsync( monitor );
                    case BasicControlDeviceOperation.ResetControllerKey: return SetControllerKeyAsync( monitor, b.ControllerKey );
                    case BasicControlDeviceOperation.None: return Task.CompletedTask;
                    default: throw new ArgumentOutOfRangeException( nameof( BasicControlDeviceCommand.Operation ) );
                }
            }
            else
            {
                return DoHandleCommandAsync( monitor, c );
            }
        }

        /// <summary>
        /// Must handle <see cref="AsyncDeviceCommand"/> command objects that are actually targeted to this device
        /// (<see cref="DeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/> and <see cref="DeviceCommand.ControllerKey"/>
        /// is either null or match the current <see cref="ControllerKey"/>).
        /// <para>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand command )
        {
            throw new ArgumentException( $"Unhandled asynchronous command {command.GetType().Name}.", nameof(command) );
        }

        /// <summary>
        /// Must handle <see cref="SyncDeviceCommand"/> command objects that are actually targeted to this device
        /// (<see cref="DeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/> and <see cref="DeviceCommand.ControllerKey"/>
        /// is either null or match the current <see cref="ControllerKey"/>).
        /// <para>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        protected virtual void DoHandleCommand( IActivityMonitor monitor, SyncDeviceCommand command )
        {
            throw new ArgumentException( $"Unhandled synchronous command {command.GetType().Name}.", nameof( command ) );
        }

        /// <summary>
        /// Overridden to return the <see cref="FullName"/>.
        /// </summary>
        /// <returns>This device's FullName.</returns>
        public override string ToString() => FullName;
    }

}
