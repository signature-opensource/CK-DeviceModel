# The Daemon and the IDeviceAlwaysRunningPolicy service

The [DeviceHostDaemon](DeviceHostDaemon.cs) is an automatically started background service (ISingletonAutoService, IHostedService).

All available hosts are injected ([IDeviceHost](../Host/IDeviceHost.cs) are marked with [IsMultiple]): it
manages all the [devices](../Device) of all the hosts by watching the devices that are Stopped whereas their configured Status
is `AlwaysRunning`.

The global [IDeviceAlwaysRunningPolicy](IDeviceAlwaysRunningPolicy.cs) service that is in charge of trying to restart the devices
is also injected.

The [DefaultDeviceAlwaysRunningPolicy](DefaultDeviceAlwaysRunningPolicy.cs) is a simple default global implementation. Being a ISingletonAutoService,
it can easily be specialized or replaced with any specific implementation.

This global policy applies to all the devices regardless of the their host. But each [DeviceHost](../Host/DeviceHost.TrackAlwaysRunning.cs)
can override the following method to handle the restart of its devices if needed:

```csharp
/// <summary>
/// Extension point that enables this host to handle its own <see cref="DeviceConfigurationStatus.AlwaysRunning"/> retry policy.
/// <para>
/// This default implementation is a simple relay to the <paramref name="global"/> <see cref="IDeviceAlwaysRunningPolicy.RetryStartAsync"/>
/// method.
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
protected virtual Task<int> TryAlwaysRunningRestart( IActivityMonitor monitor, IDeviceAlwaysRunningPolicy global, IDevice device, int retryCount )
{
    return global.RetryStartAsync( monitor, this, device, retryCount );
}
```
