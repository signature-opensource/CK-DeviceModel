using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for standard commands that a device can handle synchronously.
    /// </summary>
    public abstract class SyncDeviceCommand<THost> : SyncDeviceCommand where THost : IDeviceHost
    {
        /// <inheritdoc />
        public override Type HostType => typeof(THost);
    }
}
