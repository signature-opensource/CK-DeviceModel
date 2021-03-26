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
        DeviceCommand<DeviceApplyConfigurationResult> CreateReconfigureCommand( string name );

        StartDeviceCommand CreateStartCommand( string name );

        StopDeviceCommand CreateStopCommand( string name, bool ignoreAlwaysRunning );

        DestroyDeviceCommand CreateDestroyCommand( string name );

        SetControllerKeyDeviceCommand CreateSetControllerKeyDeviceCommand( string name, string? current, string? newControllerKey );

        Task OnDeviceConfiguredAsync( IActivityMonitor commandMonitor, IDevice device, DeviceApplyConfigurationResult applyResult, DeviceConfiguration externalConfig );

        Task OnDeviceDestroyedAsync( IActivityMonitor commandMonitor, IDevice device );

        void OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor );

        void SetDaemon( DeviceHostDaemon daemon );

        ValueTask<long> DaemonCheckAlwaysRunningAsync( IActivityMonitor monitor, DateTime now );
    }
}
