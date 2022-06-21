using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Only used to trigger the static initialization (to avoid multiple calls
    /// due to template parameters of DeviceHost).
    /// We use this as the _reconfigureSyncLock object of DeviceHost to ensure static initialization.
    /// </summary>
    class DeviceHostLock
    {
        static DeviceHostLock()
        {
            ActivityMonitor.Tags.AddDefaultFilter( IDeviceHost.DeviceModel, new LogClamper( LogFilter.Monitor, true ) );
        }
    }
}
