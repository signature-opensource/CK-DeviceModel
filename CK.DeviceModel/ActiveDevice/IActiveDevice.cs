using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non-generic active device interface that exposes an untyped <see cref="AllEvent"/> event
    /// that merges device's life time events and active device's specific events.
    /// </summary>
    public interface IActiveDevice : IDevice
    {
        /// <summary>
        /// Untyped event that merges <see cref="Device{TConfiguration}.LifetimeEvent"/> and
        /// typed <see cref="ActiveDevice{TConfiguration, TEvent}.DeviceEvent"/>.
        /// <para>
        /// Events are always raised on the event loop: LifeTimeEvent are marshalled to the
        /// internal event channel so that this stream of all events (for one device of course),
        /// is guaranteed to be serialized.
        /// </para>
        /// </summary>
        PerfectEvent<BaseDeviceEvent> AllEvent { get; }

    }
}
