using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    interface IInternalDevice : IDevice
    {
        DeviceConfigurationStatus ConfigStatus { get; }

        void OnCommandCompleted( BaseDeviceCommand cmd );
    }
}
