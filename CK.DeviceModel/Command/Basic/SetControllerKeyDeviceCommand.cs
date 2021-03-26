using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="HostedSetControllerKeyDeviceCommand{THost}"/> command that
    /// attempts to set the <see cref="IDevice.ControllerKey"/>.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="HostedSetControllerKeyDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class SetControllerKeyDeviceCommand : DeviceCommand<bool>
    {
        private protected SetControllerKeyDeviceCommand() { }

        /// <summary>
        /// Gets or sets the new key <see cref="IDevice.ControllerKey"/>.
        /// </summary>
        public string? NewControllerKey { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the controller key can obviously be changed while the device is stopped.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;


    }

}
