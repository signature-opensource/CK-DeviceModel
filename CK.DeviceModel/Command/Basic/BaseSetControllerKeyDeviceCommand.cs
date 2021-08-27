using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="SetControllerKeyDeviceCommand{THost}"/> command that
    /// attempts to set the <see cref="IDevice.ControllerKey"/>.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="SetControllerKeyDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class BaseSetControllerKeyDeviceCommand : DeviceCommandWithResult<bool>
    {
        private protected BaseSetControllerKeyDeviceCommand() { }

        /// <summary>
        /// Gets or sets the new key <see cref="IDevice.ControllerKey"/>.
        /// </summary>
        public string? NewControllerKey { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the controller key can obviously be changed while the device is stopped.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/>: the controller key can obviously be changed while the device is stopped.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

    }

}
