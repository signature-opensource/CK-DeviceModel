using CK.Core;
using System;

namespace CK.DeviceModel.Tests;

public class SimpleScaleHost : DeviceHost<SimpleScale, DeviceHostConfiguration<CommonScaleConfiguration>, CommonScaleConfiguration>
{
    protected override Type? FindDeviceTypeByConvention( IActivityMonitor monitor, Type typeConfiguration )
    {
        return typeof( SimpleScale );
    }

}
