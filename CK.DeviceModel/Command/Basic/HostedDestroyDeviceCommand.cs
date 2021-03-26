using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to destroy a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class HostedDestroyDeviceCommand<THost> : DestroyDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="HostedDestroyDeviceCommand{THost}"/>.
        /// </summary>
        public HostedDestroyDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
