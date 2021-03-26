using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base command class that exposes the host that must handle it.
    /// This class cannot be directly specialized: the generic <see cref="HostedDeviceCommand{THost}"/>
    /// must be used, or the <see cref="HostedDeviceCommand{THost,TResult}"/> when the command generates
    /// a result.
    /// </summary>
    public abstract class DeviceCommandBase
    {
        private protected DeviceCommandBase() { DeviceName = String.Empty; }

        /// <summary>
        /// Gets the type of the host for the command.
        /// </summary>
        public abstract Type HostType { get; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/> since most of the commands
        /// should not be executed while the device is stopped.
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, DeviceCommandBase)"/> protected method.
        /// </para>
        /// </summary>
        protected internal virtual DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel;

        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDeviceHost.ExecuteCommandAsync(Core.IActivityMonitor, DeviceCommand)"/> requires this name to
        /// be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Note that when this command is submitted to <see cref="IDeviceHost.ExecuteCommandAsync(IActivityMonitor, DeviceCommand)"/>, this
        /// name must not be null nor empty (and, more generally, <see cref="CheckValidity(IActivityMonitor)"/> must return true).
        /// </para>
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the required controller key. See <see cref="IDevice.ControllerKey"/>
        /// and <see cref="IDeviceHost.SendCommand(IActivityMonitor, DeviceCommandBase, System.Threading.CancellationToken)"/>.
        /// <para>
        /// Note that if the target <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey { get; set; }

        /// <summary>
        /// Checks the validity of this command. <see cref="DeviceName"/> must not be null.
        /// This calls the protected <see cref="DoCheckValidity(IActivityMonitor)"/> that should be overridden to
        /// check specific command parameters constraints.
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

            }
            return DoCheckValidity( monitor );
        }

        /// <summary>
        /// Waiting for covariant return type in .Net 5: this could be public virtual.
        /// </summary>
        internal abstract ICommandCompletionSource InternalCompletion { get; }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }
}
