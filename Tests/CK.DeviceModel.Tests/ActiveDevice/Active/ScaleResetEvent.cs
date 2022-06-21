using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Raised whenever the scale has been actually reset. 
    /// </summary>
    public sealed class ScaleResetEvent : ScaleEvent
    {
        internal ScaleResetEvent( Scale device )
            : base( device, "Reset" )
        {
        }
    }
}
