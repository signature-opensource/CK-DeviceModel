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
        DeviceCommandWithResult<DeviceApplyConfigurationResult> CreateReconfigureCommand( string name );

        BaseStartDeviceCommand CreateStartCommand( string name );

        BaseStopDeviceCommand CreateStopCommand( string name, bool ignoreAlwaysRunning );

        BaseDestroyDeviceCommand CreateDestroyCommand( string name );

        BaseSetControllerKeyDeviceCommand CreateSetControllerKeyDeviceCommand( string name, string? current, string? newControllerKey );

        bool OnDeviceConfigured( IActivityMonitor monitor, IDevice device, DeviceApplyConfigurationResult result, DeviceConfiguration externalConfig );

        bool OnDeviceDestroyed( IActivityMonitor monitor, IDevice device );

        void OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor );

        Task RaiseDevicesChangedEvent( IActivityMonitor monitor );

        void SetDaemon( DeviceHostDaemon daemon );

        ValueTask<long> DaemonCheckAlwaysRunningAsync( IActivityMonitor monitor, DateTime now );
    }
}
