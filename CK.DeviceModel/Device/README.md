# Device & StdDevice

The device base class that should be used is the StdDevice. Its implementation has been
split into two parts: the base device is only responsible of the device life cycle and
the StdDevice extends it to support commands (sync to async adaptation and command/result correlation).

## Device
A device basically exposes its [IDevice](IDevice.cs) interface that publishes its status ([DeviceStatus](DeviceStatus.cs)
and [DeviceConfigurationStatus](../DeviceConfigurationStatus.cs)) and a StatusChanged event), gives its name and its current
controller key and supports asynchronous Start, Stop and ExecuteCommand methods.
Configuration (and dynamic reconfiguration) is handled by the [IDeviceHost](../Host/IDeviceHost.cs).

The Device implementation handles the nitty-gritty details of device life cycle but doesn't provide
any implementation for really safe asynchronous command handling, independent monitoring or any events
other than StatusChanged: this is the job of the StdDevice base class.

Specialized devices must provide implementations for:

```csharp
protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );
protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );
protected abstract Task DoDestroyAsync( IActivityMonitor monitor );

```










