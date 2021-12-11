using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{

    /// <summary>
    /// Command used to apply a <see cref="BaseConfigureDeviceCommand{TConfiguration}.Configuration"/> on a device
    /// or a host.
    /// </summary>
    /// <typeparam name="THost">The type of the device host.</typeparam>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public sealed class ConfigureDeviceCommand<THost, TConfiguration> : BaseConfigureDeviceCommand<TConfiguration>
        where THost : IDeviceHost
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Initializes a new configure command with an empty configuration object.
        /// </summary>
        public ConfigureDeviceCommand()
            : this( null )
        {
        }

        /// <summary>
        /// Initializes a configure command with an existing configuration or an empty configuration object.
        /// </summary>
        /// <param name="configuration">The existing configuration or null to instantiate a new empty configuration object.</param>
        public ConfigureDeviceCommand( TConfiguration? configuration )
            : base( configuration, null )
        {
        }

        /// <summary>
        /// Overridden to return the type of the <typeparamref name="THost"/>.
        /// </summary>
        public override Type HostType => typeof(THost);
    }
}
