using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    internal interface IDeviceHost
    {
        Task<bool> TryStartAsync(IDevice d, IActivityMonitor monitor);

        Task<bool> TryStopAsync(IDevice d, IActivityMonitor monitor);
    }
}
