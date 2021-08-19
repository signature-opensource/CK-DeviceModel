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

        /// <summary>
        /// Overridden to return a string with the <see cref="IDevice.FullName"/> and <see cref="ControllerKey"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Device '{Device.FullName}' ControllerKey changed: {ControllerKey ?? "<null>"}.";

    }
}
