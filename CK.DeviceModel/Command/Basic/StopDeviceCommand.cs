using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to stop a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class StopDeviceCommand<THost> : BaseStopDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="StopDeviceCommand{THost}"/>.
        /// </summary>
        public StopDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
