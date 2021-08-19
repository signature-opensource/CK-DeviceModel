using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for <see cref="DeviceStatusChangedEvent"/>, <see cref="DeviceControllerKeyChangedEvent"/>
    /// and <see cref="DeviceConfigurationChangedEvent"/>.
    /// </summary>
    public abstract class BaseDeviceEvent
    {
        /// <summary>
        /// Initializes a new <see cref="BaseDeviceEvent"/> with its originating device.
        /// </summary>
        /// <param name="device">The device that raised this event.</param>
        protected BaseDeviceEvent( IDevice device )
        {
            if( device == null ) throw new ArgumentNullException( nameof( device ) );
            Device = device;
        }

        /// <summary>
        /// Gets the device that raised this event.
        /// </summary>
        public IDevice Device { get; }
    }
}
