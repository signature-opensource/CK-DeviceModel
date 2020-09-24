using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for standard commands that a device can handle synchronously.
    /// </summary>
    public abstract class SyncDeviceCommand
    {
        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDevice.HandleCommand(Core.IActivityMonitor, SyncDeviceCommand)"/> requires this
        /// name to be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the required <see cref="IDevice.ControllerKey"/>.
        /// When not null, <see cref="IDevice.HandleCommand(Core.IActivityMonitor, SyncDeviceCommand)"/> requires
        /// this key to be the current controller key otherwise the command is ignored.
        /// </summary>
        public string? ControllerKey { get; set; }
    }
}
