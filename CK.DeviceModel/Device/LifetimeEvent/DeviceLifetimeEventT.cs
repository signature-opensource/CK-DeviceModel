using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever a device's configuration changed.
    /// </summary>
    public sealed class DeviceLifetimeEvent<TConfiguration> : DeviceLifetimeEvent
        where TConfiguration : DeviceConfiguration
    {
        internal DeviceLifetimeEvent( IDevice device )
            : base( device )
        {
        }

        /// <summary>
        /// Gets the device configuration.
        /// </summary>
        public new TConfiguration Configuration => (TConfiguration)base.Configuration;
    }
}
