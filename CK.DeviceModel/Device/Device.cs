using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

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
        bool _isRunning;

        readonly SequentialEventHandlerSender<IDevice, DeviceStateChangedEvent> _eSeqClosed = new SequentialEventHandlerSender<IDevice, DeviceStateChangedEvent>();

        readonly SequentialEventHandlerAsyncSender<IDevice, DeviceStateChangedEvent> _eSeqClosedAsync = new SequentialEventHandlerAsyncSender<IDevice, DeviceStateChangedEvent>();

        readonly ParallelEventHandlerAsyncSender<IDevice, DeviceStateChangedEvent> _eParClosedAsync = new ParallelEventHandlerAsyncSender<IDevice, DeviceStateChangedEvent>();

        public event SequentialEventHandler<IDevice, DeviceStateChangedEvent> StateChanged
        {
            add => _eSeqClosed.Add( value );
            remove => _eSeqClosed.Remove( value );
        }
        public event SequentialEventHandlerAsync<IDevice, DeviceStateChangedEvent> StateChangedAsync
        {
            add => _eSeqClosedAsync.Add( value );
            remove => _eSeqClosedAsync.Remove( value );
        }
        public event ParallelEventHandlerAsync<IDevice, DeviceStateChangedEvent> StateChangedParallelAsync
        {
            add => _eParClosedAsync.Add( value );
            remove => _eParClosedAsync.Add( value );
        }

        Task RaiseStateChangedAsync( IActivityMonitor monitor, DeviceStateChangedEvent e )
        {
            try
            {
                Task task = _eParClosedAsync.RaiseAsync( monitor, this, e );
                _eSeqClosed.Raise( monitor, this, e );
                return Task.WhenAll( task, _eSeqClosedAsync.RaiseAsync( monitor, this, e ) );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While raising '{FullName}' {e}.", ex );
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Initializes a new device bound to a configuration.
        /// Concrete device must expose a constructor with the exact same signature: initial configuration is handled by
        /// this constructor, warnings or errors must be logged and exception can be thrown if anything goes wrong. 
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The initial configuration to use.</param>
        protected Device( IActivityMonitor monitor, TConfiguration config )
        {
            Name = config.Name;
            _configStatus = config.Status;
            FullName = null!;
        }

        internal void HostSetHost( IInternalDeviceHost host )
        {
            Debug.Assert( _host == null );
            _host = host;
            FullName = host.DeviceHostName + '/' + Name;
        }

        internal Task HostDestroyAsync( IActivityMonitor monitor )
        {
            Debug.Assert( _host != null && !_isRunning );
            _host = null;
            FullName += " (Detached)";
            return DoDestroyAsync( monitor );
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
        /// Gets whether this device has been started.
        /// From the implementation methods (<see cref="DoReconfigureAsync"/>, <see cref="DoStartAsync"/>,
        /// <see cref="DoStopAsync"/> and <see cref="DoDestroyAsync"/>) this property value is stable and can be trusted.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the current configuration status of this device.
        /// From the implementation methods (<see cref="DoReconfigureAsync"/>, <see cref="DoStartAsync"/>,
        /// <see cref="DoStopAsync"/> and <see cref="DoDestroyAsync"/>) this property value is stable and can be trusted.
        /// </summary>
        public DeviceConfigurationStatus ConfigurationStatus => _configStatus;

        internal async Task<DeviceApplyConfigurationResult> HostReconfigureAsync( IActivityMonitor monitor, TConfiguration config )
        {
            Debug.Assert( config.Name == Name );

            if( _isRunning && config.Status == DeviceConfigurationStatus.Disabled )
            {
                _configStatus = DeviceConfigurationStatus.Disabled;
                await HostStopAsync( monitor, DeviceStoppedReason.StoppedByDisabledConfiguration );
                Debug.Assert( _isRunning == false, "DoStop DOES stop." );
            }
            else
            {
                _configStatus = config.Status;
            }
            DeviceReconfiguredResult r; 
            try
            {
                r = await DoReconfigureAsync( monitor, config );
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                r = DeviceReconfiguredResult.UpdateFailed;
            }

            await RaiseStateChangedAsync( monitor, new DeviceStateChangedEvent( r ) );

            DeviceApplyConfigurationResult applyResult = (DeviceApplyConfigurationResult)r;
            if( (r == DeviceReconfiguredResult.UpdateSucceeded || r == DeviceReconfiguredResult.None)
                && !_isRunning
                && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
            {
                if( !await HostStartAsync( monitor, DeviceStartedReason.StartedByAlwaysRunningConfiguration ) )
                {
                    applyResult = DeviceApplyConfigurationResult.UpdateSucceededButStartFailed;
                }
            }
            return applyResult;
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
                return true;
            }
            return null;
        }

        internal async Task<bool> HostStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            Debug.Assert( _host != null );
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
            if( _isRunning ) await RaiseStateChangedAsync( monitor, new DeviceStateChangedEvent( reason ) );
            return _isRunning;
        }

        /// <summary>
        /// Implements this device's Start behavior.
        /// False must be returned if anything prevents this device to start (this can throw).
        /// </summary>
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
            using( monitor.OpenInfo( $"Stopping {FullName} ({reason})" ) )
            {
                if( reason == DeviceStoppedReason.StoppedByDisabledConfiguration || reason == DeviceStoppedReason.StoppedBeforeDestroy )
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
            }
            await RaiseStateChangedAsync( monitor, new DeviceStateChangedEvent( reason ) );
            return true;
        }

        /// <summary>
        /// Implements this device's Stop behavior.
        /// This should always succeed: after having called this method (that may throw), this device is considered stopped.
        /// Note that this method is never called if this device must be <see cref="DeviceConfigurationStatus.AlwaysRunning"/>
        /// (except with the <see cref="DeviceStoppedReason."/>) or it is already stopped.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reason">The reason to stop.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );

        /// <summary>
        /// Extension point to cleanup device's resources if required.
        /// This method does nothing at this level.
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
        protected virtual Task DoDestroyAsync( IActivityMonitor monitor ) => Task.CompletedTask;

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
        /// that has been configure to be always running is actually stopped.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ignoreAlwaysRunning">True to stop even if <see cref="ConfigurationStatus"/> states that this device must always run.</param>
        /// <returns>Always true except if <paramref name="ignoreAlwaysRunning"/> is false and the configuration is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.</returns>
        protected Task<bool> AutoStopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false )
        {
            return _host?.AutoStopAsync( this, monitor, ignoreAlwaysRunning ) ?? Task.FromResult(true);
        }

        /// <summary>
        /// Overridden to return the <see cref="FullName"/>.
        /// </summary>
        /// <returns>This device's FullName.</returns>
        public override string ToString() => FullName;
    }

}
