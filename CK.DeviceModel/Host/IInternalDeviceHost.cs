using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Internal interface that define non-generic host behaviors: Devices
    /// call these methods.
    /// </summary>
    internal interface IInternalDeviceHost : IDeviceHost
    {
        Task<bool> StartAsync( IDevice d, IActivityMonitor monitor );

        Task<bool> StopAsync( IDevice d, IActivityMonitor monitor );

        Task<bool> AutoStopAsync( IDevice d, IActivityMonitor monitor, bool ignoreAlwaysRunning );

        Task AutoDestroyAsync( IDevice d, IActivityMonitor monitor );
    }
}
