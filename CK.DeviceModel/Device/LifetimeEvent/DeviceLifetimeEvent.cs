using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever a device's configuration changed.
    /// </summary>
    public abstract class DeviceLifetimeEvent : BaseDeviceEvent
    {
        private protected DeviceLifetimeEvent( IDevice device )
            : base( device )
        {
            DeviceStatus = device.Status;
            Configuration = device.ExternalConfiguration;
            ControllerKey = device.ControllerKey;
        }

        /// <summary>
        /// Gets the device status.
        /// </summary>
        public DeviceStatus DeviceStatus { get; }

        /// <summary>
        /// Gets the device configuration.
        /// </summary>
        public DeviceConfiguration Configuration { get; }

        /// <summary>
        /// Gets the device's controller key.
        /// </summary>
        public string? ControllerKey { get; }

        /// <summary>
        /// Gets whether the <see cref="DeviceStatus"/> has changed.
        /// </summary>
        public bool StatusChanged { get; internal set; }

        /// <summary>
        /// Gets whether the <see cref="Configuration"/> has changed.
        /// </summary>
        public bool ConfigurationChanged { get; internal set; }

        /// <summary>
        /// Gets whether the <see cref="ControllerKey"/> has changed.
        /// </summary>
        public bool ControllerKeyChanged { get; internal set; }

        /// <summary>
        /// Overridden to return a string with the <see cref="IDevice.FullName"/> and <see cref="DeviceStatus"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString()
        {
            var s = StatusChanged ? $"Device '{Device.FullName}' status changed: {DeviceStatus}." : "";
            if( ControllerKeyChanged )
            {
                if( s.Length > 0 ) s += ' ';
                s += "ControllerKey changed.";
            }
            if( ConfigurationChanged )
            {
                if( s.Length > 0 ) s += ' ';
                s += "Configuration changed.";
            }
            return s;
        }
    }
}
