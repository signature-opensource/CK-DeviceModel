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
        /// <see cref="K0010Device.HandleCommand(CK.Core.IActivityMonitor, object)"/> requires this
        /// name to be the one of the device (see <see cref="CK.DeviceModel.IDevice.Name"/>) otherwise
        /// the command is ignored.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the required <see cref="CK.DeviceModel.IDevice.ControllerKey"/>.
        /// When not null, <see cref="K0010Device.HandleCommand(CK.Core.IActivityMonitor, object)"/> requires
        /// this key to be the current controller key otherwise the command is ignored.
        /// </summary>
        public string? ControllerKey { get; set; }
    }
}
