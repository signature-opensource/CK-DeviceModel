using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to set a new <see cref="IDevice.ControllerKey"/>.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class HostedSetControllerKeyDeviceCommand<THost> : SetControllerKeyDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="HostedSetControllerKeyDeviceCommand{THost}"/>.
        /// </summary>
        public HostedSetControllerKeyDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
