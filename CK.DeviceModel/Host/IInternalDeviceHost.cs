using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Internal interface that define non-generic host behaviors: Devices and DeviceHostDaemon
    /// call these methods.
    /// </summary>
    interface IInternalDeviceHost : IDeviceHost
    {
        Task<bool> StartAsync( IDevice d, IActivityMonitor monitor, bool autoStart );

        Task<bool> StopAsync( IDevice d, IActivityMonitor monitor );

        Task<bool> AutoStopAsync( IDevice d, IActivityMonitor monitor, bool ignoreAlwaysRunning );

        Task<bool> SetControllerKeyAsync( IDevice d, IActivityMonitor monitor, bool checkCurrent, string? current, string? key );

        Task AutoDestroyAsync( IDevice d, IActivityMonitor monitor );

        void OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor );

        void SetDaemon( DeviceHostDaemon daemon );

        ValueTask<long> CheckAlwaysRunningAsync( IActivityMonitor monitor, DateTime now );
    }
}
