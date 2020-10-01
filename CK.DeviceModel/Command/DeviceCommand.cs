using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic interface that exposes the host that handles any <see cref="SyncDeviceCommand"/> or <see cref="AsyncDeviceCommand"/>.
    /// </summary>
    public abstract class DeviceCommand
    {
        private protected DeviceCommand() { }

        /// <summary>
        /// Gets the type of the host for the command.
        /// </summary>
        public abstract Type HostType { get; }

        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDeviceHost.Handle(Core.IActivityMonitor, DeviceCommand)"/> requires this name to be the one of the device (see <see cref="IDevice.Name"/>)
        /// otherwise the command is ignored.
        /// <para>
        /// Note that when handling this command is submitted to <see cref="IDeviceHost.Handle(IActivityMonitor, DeviceCommand)"/>, this
        /// name must not be null nor empty (and, more generally, <see cref="CheckValidity(IActivityMonitor)"/> must return true).
        /// </para>
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the required controller key. See <see cref="IDevice.ControllerKey"/> and <see cref="IDeviceHost.Handle(IActivityMonitor, DeviceCommand)"/>.
        /// <para>
        /// Note that if the target <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey { get; set; }

        /// <summary>
        /// Checks the validity of this command. <see cref="DeviceName"/> must not be null nor empty.
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <returns>Whether this configuration is valid.</returns>
        public bool CheckValidity( IActivityMonitor monitor )
        {
            if( String.IsNullOrEmpty( DeviceName ) )
            {
                monitor.Error( $"Command '{GetType().Name}': DeviceName must not be null or empty." );
                return false;
            }
            if( HostType == null )
            {
                monitor.Error( $"Command '{GetType().Name}': HostType is null." );
                return false;
            }
            return DoCheckValidity( monitor );
        }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }
}
