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
    /// <para>
    /// This is public because IDeviceHost is a IAutoService (also [IsMultiple]) and so it must be public...
    /// Internal interface specializations should be allowed: this public is temporary.
    /// </para>
    /// </summary>
    public interface IInternalDeviceHost : IDeviceHost
    {
        Task<bool> StartAsync( IDevice d, IActivityMonitor monitor );

        Task<bool> StopAsync( IDevice d, IActivityMonitor monitor );

        Task<bool> AutoStopAsync( IDevice d, IActivityMonitor monitor, bool ignoreAlwaysRunning );

        Task<bool> SetControllerKeyAsync( IDevice d, IActivityMonitor monitor, bool checkCurrent, string? current, string? key );

        Task AutoDestroyAsync( IDevice d, IActivityMonitor monitor );

    }
}
