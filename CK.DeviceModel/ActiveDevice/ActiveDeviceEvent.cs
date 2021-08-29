using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for all active device events.
    /// </summary>
    public abstract class ActiveDeviceEvent<TDevice> : BaseDeviceEvent where TDevice : IActiveDevice
    {
        /// <summary>
        /// Initializes a new <see cref="ActiveDeviceEvent"/> with its originating device.
        /// </summary>
        /// <param name="device">The device that raised this event.</param>
        protected ActiveDeviceEvent( TDevice device )
            : base( device )
        {
        }

        /// <summary>
        /// Gets the active <typeparamref name="TDevice"/> that raised this event.
        /// </summary>
        public new TDevice Device => (TDevice)base.Device;

    }
}
