using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

public sealed class ScaleMeasureEvent : ScaleEvent, ICommonScaleMeasureEvent
{
    internal ScaleMeasureEvent( Scale device, double measure, string text )
        : base( device, text )
    {
        Measure = measure;
    }

    public double Measure { get; }
}
