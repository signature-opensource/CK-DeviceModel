using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{

    /// <summary>
    /// Command used to apply a <see cref="ReconfigureDeviceCommand{TConfiguration}.Configuration"/> on a device.
    /// </summary>
    /// <typeparam name="THost">The type of the device host.</typeparam>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public class HostedReconfigureDeviceCommand<THost, TConfiguration> : ReconfigureDeviceCommand<TConfiguration>
        where THost : IDeviceHost
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Overridden to return the type of the <typeparamref name="THost"/>.
        /// </summary>
        public override Type HostType => typeof(THost);
    }
}
