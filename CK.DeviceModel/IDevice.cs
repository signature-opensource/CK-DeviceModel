using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    internal interface IDevice
    {
        string Name { get; }

        DeviceConfigurationStatus ConfigurationStatus { get; }
    }

}
