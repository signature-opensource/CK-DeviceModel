using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for <see cref="DeviceStatusChangedEvent"/>, <see cref="DeviceControllerKeyChangedEvent"/>
    /// and <see cref="DeviceConfigurationChangedEvent"/>.
    /// </summary>
    public abstract class DeviceLifetimeEvent : BaseDeviceEvent
    {
        private protected DeviceLifetimeEvent( IDevice device )
            : base( device )
        {
        }
    }
}
