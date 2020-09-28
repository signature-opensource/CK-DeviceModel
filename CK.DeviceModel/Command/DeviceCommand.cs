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
        /// <see cref="IDevice.HandleCommand(Core.IActivityMonitor, SyncDeviceCommand)"/> and <see cref="IDevice.HandleCommandAsync(Core.IActivityMonitor, AsyncDeviceCommand)"/>
        /// require this name to be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the required controller key.
        /// <see cref="IDevice.HandleCommand(Core.IActivityMonitor, SyncDeviceCommand)"/> and <see cref="IDevice.HandleCommandAsync(Core.IActivityMonitor, AsyncDeviceCommand)"/>
        /// require this key to be the current <see cref="IDevice.ControllerKey"/> otherwise the command is ignored.
        /// <para>
        /// Note that if the <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey { get; set; }
    }
}
