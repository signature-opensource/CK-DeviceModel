using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever a device's configuration changed.
    /// </summary>
    public class DeviceConfigurationChangedEvent : DeviceLifetimeEvent
    {
        internal DeviceConfigurationChangedEvent( IDevice device, DeviceConfiguration c )
            : base( device )
        {
            Configuration = c;
        }

        /// <summary>
        /// Gets the new configuration.
        /// </summary>
        public DeviceConfiguration Configuration { get; }
    }
}
