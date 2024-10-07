using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

public class SimpleScaleHost : DeviceHost<SimpleScale, DeviceHostConfiguration<CommonScaleConfiguration>, CommonScaleConfiguration>
{
    protected override Type? FindDeviceTypeByConvention( IActivityMonitor monitor, Type typeConfiguration )
    {
        return typeof( SimpleScale );
    }

}
