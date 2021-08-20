using CK.Core;
using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// This class cannot be directly specialized: the generic <see cref="ConfigureDeviceCommand{THost,TConfiguration}"/>
    /// must be used.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract class BaseConfigureDeviceCommand<TConfiguration> : BaseConfigureDeviceCommand
        where TConfiguration : DeviceConfiguration
    {
        private protected BaseConfigureDeviceCommand( TConfiguration? configuration )
            : base( configuration ?? Activator.CreateInstance<TConfiguration>() )
        {
        }

        internal TConfiguration? ExternalConfig { get; set; }

        /// <summary>
        /// Gets the configuration to apply.
        /// </summary>
        public new TConfiguration Configuration => (TConfiguration)base.Configuration;

    }
}
