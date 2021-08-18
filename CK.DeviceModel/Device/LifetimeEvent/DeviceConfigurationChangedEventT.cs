using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever a device's configuration changed.
    /// </summary>
    public sealed class DeviceConfigurationChangedEvent<TConfiguration> : DeviceConfigurationChangedEvent
        where TConfiguration : DeviceConfiguration
    {
        internal DeviceConfigurationChangedEvent( IDevice device, TConfiguration c )
            : base( device, c )
        {
        }

        /// <summary>
        /// Gets the new configuration.
        /// </summary>
        public new TConfiguration Configuration => (TConfiguration)base.Configuration;
    }
}
