using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

public class ScaleHost : DeviceHost<Scale, DeviceHostConfiguration<CommonScaleConfiguration>, CommonScaleConfiguration>
{
    protected override Type? FindDeviceTypeByConvention( IActivityMonitor monitor, Type typeConfiguration )
    {
        return typeof( Scale );
    }
}
