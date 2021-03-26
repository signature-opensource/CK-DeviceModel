using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to start a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class HostedStartDeviceCommand<THost> : StartDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="HostedStartDeviceCommand{THost}"/>.
        /// </summary>
        public HostedStartDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
