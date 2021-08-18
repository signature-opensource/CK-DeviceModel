using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever the <see cref="IDevice.Status"/> changed.
    /// </summary>
    public sealed class DeviceStatusChangedEvent : DeviceLifetimeEvent
    {
        internal DeviceStatusChangedEvent( IDevice device, DeviceStatus status )
            : base( device )
        {
            Status = status;
        }

        /// <summary>
        /// Gets the new status.
        /// </summary>
        public DeviceStatus Status { get; }
    }
}
