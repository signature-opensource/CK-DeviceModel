using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands with result that a device can handle.
    /// </summary>
    public abstract class HostedDeviceCommand<THost,TResult> : DeviceCommand<TResult> where THost : IDeviceHost
    {
        /// <inheritdoc />
        public override Type HostType => typeof(THost);
    }
}
