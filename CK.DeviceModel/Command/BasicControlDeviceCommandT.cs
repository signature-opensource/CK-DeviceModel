using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Command
{
    /// <summary>
    /// Single command that supports the basic device operations.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class BasicControlDeviceCommand<THost> : BasicControlDeviceCommand where THost : IDeviceHost
    {
        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
