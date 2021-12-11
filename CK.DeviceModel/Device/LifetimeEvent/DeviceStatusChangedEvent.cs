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

        /// <summary>
        /// Overridden to return a string with the <see cref="IDevice.FullName"/> and <see cref="Status"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Device '{Device.FullName}' status changed: {Status}.";
    }
}
