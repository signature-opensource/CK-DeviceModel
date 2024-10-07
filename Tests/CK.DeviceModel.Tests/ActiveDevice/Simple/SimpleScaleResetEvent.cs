using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

/// <summary>
/// Raised whenever the scale has been actually reset. 
/// </summary>
public sealed class SimpleScaleResetEvent : SimpleScaleEvent
{
    internal SimpleScaleResetEvent( SimpleScale device )
        : base( device, "Reset" )
    {
    }
}
