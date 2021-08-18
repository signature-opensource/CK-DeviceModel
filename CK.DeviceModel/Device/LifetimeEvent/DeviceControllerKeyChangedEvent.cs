using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Event raised whenever the <see cref="IDevice.ControllerKey"/> changed.
    /// </summary>
    public sealed class DeviceControllerKeyChangedEvent : DeviceLifetimeEvent
    {
        internal DeviceControllerKeyChangedEvent( IDevice device, string? controllerKey )
            : base( device )
        {
            ControllerKey = controllerKey;
        }

        /// <summary>
        /// Gets the new controller key.
        /// </summary>
        public string? ControllerKey { get; }
    }
}
