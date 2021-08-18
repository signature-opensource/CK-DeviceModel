# Device

Devices support commands (sync to async adaptation and command/result correlation).

## Device
A device basically exposes its [IDevice](IDevice.cs) interface that publishes its status ([DeviceStatus](DeviceStatus.cs)
and [DeviceConfigurationStatus](../DeviceConfigurationStatus.cs) and a StatusChanged event), exposes its FullName and its
current controller key and supports asynchronous Start, Stop, Reconfigure, Destroy and SendCommand methods.
Configuration (and dynamic reconfiguration) is handled by its [IDeviceHost](../Host/IDeviceHost.cs).

The Device implementation handles the nitty-gritty details of device life cycle and provide
any implementation for really safe asynchronous command handling and independent monitoring thanks to an internal asynchronous loop.

Specialized devices must provide implementations for:

```csharp
protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );
protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );
protected abstract Task DoDestroyAsync( IActivityMonitor monitor );
```

## Commands

Devices can support any number of specific methods (devices can be seen as multiple instances of micro [IHostedService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostedservice)
that support dynamic reconfiguration), but their implementations SHOULD only be helpers that send Commands.

> Any device implementation MUST rely on Commands. This is the only way to prevent concurrency issues.

Key features of the Commands support are:

- Command execution is serialized thanks to an internally managed asynchronous command loop with its own ActivityMonitor and 
a [channel](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels).
- Commands can generate a result (see [DeviceCommand&lt;TResult&gt;](../Command/DeviceCommandT.cs)) or not.
- Commands completion MUST be signaled explicitly.
- Commands may transform errors or cancellation into command results. The [BaseReconfigureDeviceCommand](../Command/Basic/BaseReconfigureDeviceCommand.cs)
is an example where errors or cancellation are mapped to [DeviceApplyConfigurationResult](../Host/DeviceApplyConfigurationResult.cs) enumeration values.
- Commands that are handled while the device is stopped can be considered as errors, be canceled, be executed anyway or deferred until the device
 starts again (see the [DeviceCommandStoppedBehavior enumeration](../Command/DeviceCommandStoppedBehavior.cs).

More on Commands [here](../Command).

## Runtime properties & Configuration properties

Nothing prevents a device to expose properties. In such case, maximal care should be taken to handle concurrency. A good approach
is to expose only read-only properties and only update them through Commands.

If a configuration property that is on the [DeviceConfiguration](../DeviceConfiguration.cs) must be exposed, it should
be read-only and updated by the (re)configuration.

Devices don't expose a `ConfigurationChanged` event (note that devices don't expose their Configuration object).
This is on purpose: devices only expose the `StatusChanged` event that covers the device's lifetime status and configuration.

Any specific configuration properties can be handled explicitly with dedicated events and/or read-only properties on the device if needed.

