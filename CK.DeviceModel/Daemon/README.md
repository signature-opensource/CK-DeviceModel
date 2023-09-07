# The Daemon

The [DeviceHostDaemon](DeviceHostDaemon.cs) is an automatically started background service (ISingletonAutoService, IHostedService).

It has 4 responsibilities:
- It instantiates all the host when itself is instantiated: all available hosts are injected as a `IEnumerable<IDeviceHost>` 
([IDeviceHost](../Host/IDeviceHost.cs) is marked with `[IsMultiple]`) and since it concretizes the enumerable, all hosts are instantiated.
- It manages the "AlwaysRunning" configuration status of the devices.
- It can reconfigure a device or a host from a `IConfigurationSection`.
- It can optionally destroy all the devices when stopped (instead of let them die with the process) by 
setting the `DaemonHostDevice.StoppedBehavior` property to [OnStoppedDaemonBehavior](OnStoppedDaemonBehavior.cs)`.ClearAllHosts`
or `ClearAllHostsAndWaitForDevicesDestroyed`.


## The AlwaysRunning and IDeviceAlwaysRunningPolicy

The daemon monitors all the [devices](../Device) of all the hosts by reacting to devices that are Stopped whereas their configured Status
is `AlwaysRunning`.

The daemon relies on the global [IDeviceAlwaysRunningPolicy](IDeviceAlwaysRunningPolicy.cs) service that is in charge of trying to restart
the devices (it's a constructor dependency).

The [DefaultDeviceAlwaysRunningPolicy](DefaultDeviceAlwaysRunningPolicy.cs) is a simple default global implementation. Being a ISingletonAutoService,
it can easily be specialized or replaced with any specific implementation (it has been designed to be easily specialized).

This global policy applies to all the devices regardless of the their host. But each [DeviceHost](../Host/DeviceHost.TrackAlwaysRunning.cs)
can override the following method to handle the restart of its devices if needed:

```csharp
/// <summary>
/// Extension point that enables this host to handle its own DeviceConfigurationStatus.AlwaysRunning retry policy.
/// <para>
/// This default implementation is a simple relay to the global RetryStartAsync method.
/// </para>
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="global">The globally available policy.</param>
/// <param name="device">The faulty device.</param>
/// <param name="retryCount">
/// The number of previous attempts to restart the device (since the last time the device has stopped).
/// For the very first attempt, this is 0. 
/// </param>
/// <returns>The number of millisecond to wait before the next retry or 0 to stop retrying.</returns>
protected virtual Task<int> TryAlwaysRunningRestart( IActivityMonitor monitor,
                                                     IDeviceAlwaysRunningPolicy global,
                                                     IDevice device,
                                                     int retryCount )
{
    return global.RetryStartAsync( monitor, this, device, retryCount );
}
```
