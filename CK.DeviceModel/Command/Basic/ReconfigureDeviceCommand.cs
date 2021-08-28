using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{

    /// <summary>
    /// Command used to apply a <see cref="BaseReconfigureDeviceCommand{TConfiguration}.Configuration"/> on a device.
    /// This command is by default (like the other basic commands), sent immediately (<see cref="BaseDeviceCommand.ImmediateSending"/> is true).
    /// </summary>
    /// <typeparam name="THost">The type of the device host.</typeparam>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public class ReconfigureDeviceCommand<THost, TConfiguration> : BaseReconfigureDeviceCommand<TConfiguration>
        where THost : IDeviceHost
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Overridden to return the type of the <typeparamref name="THost"/>.
        /// </summary>
        public override Type HostType => typeof(THost);
    }
}
