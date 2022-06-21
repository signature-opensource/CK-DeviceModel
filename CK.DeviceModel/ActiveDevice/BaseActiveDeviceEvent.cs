using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non-generic base class for all active device events.
    /// This adds nothing to the <see cref="BaseDeviceEvent"/> except this type.
    /// This class cannot be directly specialized: the generic <see cref="ActiveDeviceEvent{TDevice}"/>
    /// must be used.
    /// </summary>
    public abstract class BaseActiveDeviceEvent : BaseDeviceEvent
    {
        private protected BaseActiveDeviceEvent( IDevice device )
            : base( device )
        {
        }
    }
}
