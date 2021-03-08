using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands without any result that a device can handle.
    /// </summary>
    public abstract class DeviceCommand<THost> : DeviceCommand where THost : IDeviceHost
    {
        /// <inheritdoc />
        public override Type HostType => typeof(THost);
    }
}
