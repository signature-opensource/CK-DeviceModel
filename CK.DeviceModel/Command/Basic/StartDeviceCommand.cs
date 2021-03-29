using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to start a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class StartDeviceCommand<THost> : BaseStartDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="StartDeviceCommand{THost}"/>.
        /// </summary>
        public StartDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
