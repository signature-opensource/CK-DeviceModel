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

        /// <summary>
        /// Overridden to return a string with the <see cref="IDevice.FullName"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Device '{Device.FullName}' Configuration changed.";

    }
}
