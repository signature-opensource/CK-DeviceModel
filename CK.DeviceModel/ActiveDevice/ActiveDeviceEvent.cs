using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Strongly typed base class for active device events.
    /// </summary>
    public abstract class ActiveDeviceEvent<TDevice> : BaseActiveDeviceEvent where TDevice : IActiveDevice
    {
        /// <inheritdoc />
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
