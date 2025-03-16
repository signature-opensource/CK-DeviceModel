namespace CK.DeviceModel.Tests;

public sealed class SimpleScaleMeasureEvent : SimpleScaleEvent, ICommonScaleMeasureEvent
{
    internal SimpleScaleMeasureEvent( SimpleScale device, double measure, string text )
        : base( device, text )
    {
        Measure = measure;
    }

    public double Measure { get; }
}
