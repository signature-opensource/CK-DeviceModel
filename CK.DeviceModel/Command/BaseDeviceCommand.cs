using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base command class that exposes the host that must handle it.
    /// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost}"/>
    /// must be used, or the <see cref="DeviceCommand{THost,TResult}"/> when the command generates
    /// a result.
    /// </summary>
    public abstract class BaseDeviceCommand
    {
        private string _deviceName;
        private string? _controllerKey;
        bool _isLocked;

        /// <summary>
        /// Initialize a new locked command if <paramref name="locked"/> is provided.
        /// Otherwise initializes a new unlocked command (DeviceName is empty, ControllerKey is null).
        /// </summary>
        /// <param name="locked">The device name and controller key or null.</param>
        private protected BaseDeviceCommand( (string lockedName, string? lockedControllerKey)? locked = null )
        {
            if( locked.HasValue )
            {
                (_deviceName, _controllerKey) = locked.Value;
                _isLocked = true;
            }
            else
            {
                _deviceName = String.Empty;
            }
        }

        /// <summary>
        /// Gets the type of the host for the command.
        /// </summary>
        public abstract Type HostType { get; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/> since most of the commands
        /// should not be executed while the device is stopped and this enables always running devices to be resilient to
        /// unattended stops (and subsequent restarts).
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// When <see cref="ImmediateSending"/> is true, <see cref="ImmediateStoppedBehavior"/> applies and this property is ignored.
        /// </para>
        /// </summary>
        protected internal virtual DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.Cancel"/> since most of the commands
        /// should not be executed while the device is stopped.
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceImmediateCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// This applies when <see cref="ImmediateSending"/> is true. See <see cref="StoppedBehavior"/> otherwise.
        /// </para>
        /// </summary>
        protected internal virtual DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.Cancel;

        /// <summary>
        /// Gets or sets whether this command must be sent and handled immediately.
        /// Defaults to false, except for the 5 basic commands (Start, Stop, Configure, SetControllerKey and Destroy).
        /// </summary>
        public bool ImmediateSending { get; set; }

        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/> requires this name to
        /// be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Note that when this command is sent to the device, this name must not be null nor empty (and, more generally,
        /// <see cref="CheckValidity(IActivityMonitor)"/> must return true).
        /// </para>
        /// </summary>
        public string DeviceName
        {
            get => _deviceName;
            set
            {
                if( value == null ) throw new ArgumentNullException( nameof( DeviceName ) );
                if( value != _deviceName )
                {
                    ThrowOnLocked();
                    _deviceName = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the required controller key. See <see cref="IDevice.ControllerKey"/>
        /// and <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>.
        /// <para>
        /// Note that if the target <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey
        {
            get => _controllerKey;
            set
            {
                ThrowOnLocked();
                _controllerKey = value;
            }
        }

        /// <summary>
        /// Gets whether this command has been submitted and should not be altered anymore.
        /// </summary>
        public bool IsLocked => _isLocked;

        /// <summary>
        /// Checks the validity of this command. <see cref="DeviceName"/> must not be null.
        /// This calls the protected <see cref="DoCheckValidity(IActivityMonitor)"/> that should be overridden to
        /// check specific command parameters constraints.
        /// <para>
        /// This can be called even if <see cref="IsLocked"/> is true.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <returns>Whether this configuration is valid.</returns>
        public bool CheckValidity( IActivityMonitor monitor )
        {
            Debug.Assert( HostType != null, "Thanks to the private protected constructor and the generic of <THost>, a command instance has a host type." );
            if( DeviceName == null )
            {
                monitor.Error( $"Command '{GetType().Name}': DeviceName must not be null." );
                return false;
            }
            if( InternalCompletion.IsCompleted )
            {
                monitor.Error( $"{GetType().Name} has already a Result. Command cannot be reused." );
                return false;
            }
            return DoCheckValidity( monitor );
        }

        /// <summary>
        /// Sets <see cref="IsLocked"/> to true.
        /// Called once the command is submitted (it has already been successfully validated).
        /// This method can be overridden to prepare the command (like cloning internal data).
        /// <para>
        /// Override should ensure that this method can safely be called multiple times.
        /// </para>
        /// </summary>
        public virtual void Lock()
        {
            _isLocked = true;
        }

        /// <summary>
        /// Helper method that raises an <see cref="InvalidOperationException"/> if <see cref="IsLocked"/> is true.
        /// </summary>
        protected void ThrowOnLocked()
        {
            if( _isLocked ) throw new InvalidOperationException( nameof( IsLocked ) );
        }

        /// <summary>
        /// Waiting for covariant return type in .Net 5: this could be public virtual.
        /// </summary>
        internal abstract ICompletionSource InternalCompletion { get; }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;
    }
}
