using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Internal interface that define non-generic host behaviors: Devices and DeviceHostDaemon
    /// call these methods.
    /// </summary>
    interface IInternalDeviceHost : IDeviceHost
    {
        BaseConfigureDeviceCommand CreateLockedConfigureCommand( string name,
                                                                 string? controllerKey,
                                                                 DeviceConfiguration? configuration,
                                                                 DeviceConfiguration? clonedConfiguration );

        BaseStartDeviceCommand CreateStartCommand( string name );

        BaseStopDeviceCommand CreateStopCommand( string name, bool ignoreAlwaysRunning );

        BaseDestroyDeviceCommand CreateDestroyCommand( string name );

        BaseSetControllerKeyDeviceCommand CreateSetControllerKeyDeviceCommand( string name, string? current, string? newControllerKey );

        /// <summary>
        /// Called synchronously (interact with the reconfiguring sync lock).
        /// </summary>
        bool OnDeviceDoDestroy( IActivityMonitor monitor, IDevice device );

        void DeviceOnAlwaysRunningCheck( IInternalDevice d, IActivityMonitor monitor, bool fromStart );

        Task RaiseDevicesChangedEventAsync( IActivityMonitor monitor );

        Task RaiseAllDevicesLifetimeEventAsync( IActivityMonitor monitor, DeviceLifetimeEvent e );

        Task RaiseAllDevicesEventAsync( IActivityMonitor monitor, BaseDeviceEvent e );

        void SetDaemon( DeviceHostDaemon daemon );

        ValueTask<long> DaemonCheckAlwaysRunningAsync( IActivityMonitor monitor, IDeviceAlwaysRunningPolicy global, DateTime now );

        CancellationToken DaemonStoppedToken { get; }
    }
}
