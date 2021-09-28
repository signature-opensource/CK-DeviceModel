using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non-generic active device interface that exposes an untyped <see cref="AllEvent"/> event
    /// that merges device's life time events and active device's specific events.
    /// <para>
    /// This applies to <see cref="SimpleActiveDevice{TConfiguration, TEvent}"/> and <see cref="ActiveDevice{TConfiguration, TEvent}"/>.
    /// </para>
    /// </summary>
    public interface IActiveDevice : IDevice
    {
        /// <summary>
        /// Untyped event that merges <see cref="Device{TConfiguration}.LifetimeEvent"/> and
        /// typed <see cref="IActiveDevice{TEvent}.DeviceEvent"/>.
        /// </summary>
        PerfectEvent<BaseDeviceEvent> AllEvent { get; }

        /// <summary>
        /// Posts an event in this device's event queue.
        /// Can be used to mimic or emulate a running device.
        /// This should be used with care. 
        /// <para>
        /// Event's type must match the actual <see cref="ActiveDeviceEvent{TDevice}"/> type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="e">The event to inject.</param>
        void DebugPostEvent( BaseActiveDeviceEvent e );
    }
}
