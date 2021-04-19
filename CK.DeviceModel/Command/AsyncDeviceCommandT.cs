using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for standard commands that a device can handle asynchronously.
    /// </summary>
    public abstract class AsyncDeviceCommand<THost> : AsyncDeviceCommand where THost : IDeviceHost
    {
        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }
}
