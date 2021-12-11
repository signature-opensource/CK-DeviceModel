# Basic commands

The 5 basic commands follow the same pattern:

- They cannot be specialized outside of the CK.DeviceModel.

- Their `StoppedBehavior` and `ImmediateStoppedBehavior` properties are both "RunAnyway" (they can run on a stopped device).

- Their `ImmediateSending` property is true by default.

- They all respect the `ControllerKey` optional guard.

- They can be sent via SendCommand or are simply useless since the 5 device's methods  
SetControllerKeyAsync, StartAsync, StopAsync, ReconfigureAsync or DestroyAsync (see [IDevice](../../Device/IDevice.cs)) helpers
do the job.

Specificities are:
- [Destroy](BaseDestroyDeviceCommand.cs) has no result and always succeeds (thanks to the OnError and OnCanceled implementations).
- [Start](StartDeviceCommand.cs) and [Stop](StopDeviceCommand.cs) are boolean commands. Exceptions or cancellation are transformed into a `false' result.
- Stop can specify `IgnoreAlwaysRunning` to stop a device even if its configuration state that it must be `AlwaysRunning`. The [daemon](../../Daemon) then enters into play.
- [ReconfigureDeviceCommand&lt;TConfiguration&gt;](ConfigureDeviceCommand.cs) is parametrized on the [Device](../../Device/Device.cs) and non-generic on [IDevice](../../Device/IDevice.cs)).
   Cancellations are transformed into `ConfigurationCanceled`, exceptions are transformed into `InvalidControllerKey` or `UnexpectedError` result (see [DeviceApplyConfigurationResult](../../Host/DeviceApplyConfigurationResult.cs)).
- [SetControllerKey](SetControllerKeyDeviceCommand.cs) exposes the `NewControllerKey` (in addition to the standard `ControllerKey` guard).

![The 5 basic commands](/../../../Common/Doc/BasicCommands.png)

