using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel;

interface IInternalDevice : IDevice
{
    DeviceConfigurationStatus ConfigStatus { get; }

    void OnCommandCompleted( BaseDeviceCommand cmd );

    Task EnsureInitialLifetimeEventAsync( IActivityMonitor monitor );
}
