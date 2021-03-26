using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to stop a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class HostedStopDeviceCommand<THost> : StopDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="HostedStopDeviceCommand{THost}"/>.
        /// </summary>
        public HostedStopDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
