using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to set a new <see cref="IDevice.ControllerKey"/>.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class SetControllerKeyDeviceCommand<THost> : BaseSetControllerKeyDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="SetControllerKeyDeviceCommand{THost}"/>.
        /// </summary>
        public SetControllerKeyDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
