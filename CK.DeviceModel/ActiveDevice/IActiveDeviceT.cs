using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Generic active device interface that exposes the typed <see cref="DeviceEvent"/>.
    /// <para>
    /// This applies to <see cref="ActiveDevice{TConfiguration, TEvent}"/> but also to the <see cref="SimpleActiveDevice{TConfiguration, TEvent}"/>
    /// (that is not exactly a real ActiveDevice).
    /// </para>
    /// </summary>
    public interface IActiveDevice<TEvent> : IActiveDevice
        where TEvent : BaseActiveDeviceEvent
    {
        /// <summary>
        /// Typed device event.
        /// </summary>
        PerfectEvent<TEvent> DeviceEvent { get; }
    }
}
