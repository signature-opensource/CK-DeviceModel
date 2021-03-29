using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to destroy a device.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class DestroyDeviceCommand<THost> : BaseDestroyDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="DestroyDeviceCommand{THost}"/>.
        /// </summary>
        public DestroyDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
