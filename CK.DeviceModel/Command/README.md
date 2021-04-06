# Commands

All device commands must inherit from [DeviceCommand](DeviceCommand&lt;THost&gt;.cs) or [DeviceCommand&lt;THost,TResult&gt;](DeviceCommandT.cs)
if the command generates a result.

Commands are sent to a device and are executed asynchronously by each device's command loop: developers are isolated from
any concurrency issues since all commands (including the [5 basic commands](Basic)) are handled sequentially.

## Command sending

### On the Device

Commands can be sent to a device by using the 4 available methods directly on any [IDevice](../Device/IDevice.cs) object:

```csharp
bool SendCommand( IActivityMonitor monitor,
                  BaseDeviceCommand command,
                  bool checkDeviceName = true,
                  bool checkControllerKey = true,
                  CancellationToken token = default );

bool UnsafeSendCommand( IActivityMonitor monitor,
                        BaseDeviceCommand command,
                        CancellationToken token = default );

bool SendCommandImmediate( IActivityMonitor monitor,
                           BaseDeviceCommand command,
                           bool checkDeviceName = true,
                           bool checkControllerKey = true,
                           CancellationToken token = default );

bool UnsafeSendCommandImmediate( IActivityMonitor monitor,
                                 BaseDeviceCommand command,
                                 CancellationToken token = default );
```
These methods return `false` if the device is destroyed and cannot receive commands anymore throw an `ArgumentException`
if the command is null or if its `CheckValidity` method returns `false`.

#### Safe vs. Unsafe

By default, the `BaseDeviceCommand.DeviceName` MUST match the device's name (this is checked when the command is sent
and raises an `ArgumentException` is raise),
and the `BaseDeviceCommand.ControllerKey` must match the device's current `ControllerKey` (or the latter is null).

The controller key is not checked when the command is sent but when right before the command execution (this is because a previously
sent command can change the controller key). If the controller key doesn't match, an [InvalidControllerKeyException](../Device/InvalidControllerKeyException.cs)
is set on the command completion.

This (safe) behavior can be amended thanks to the SendCommand or SendCommandImmediate parameters or by calling the Unsafe methods.

#### Immediate or not

By default, commands are queued and are executed one after the others: there is NO concurrency to handle. However, the
`SendCommandImmediate` and `UnsafeSendCommandImmediate` can be used to skip any waiting commands and to execute
the command immediately (after having wait for the end of the currently executing command).
Once the immediate command is executed, queued commands continue to execute.

Think to this *immediate* as a higher priority queue.

> The 5 [basic `IDevice` methods](Basic) (`SetControllerKeyAsync`, `StartAsync`, `StopAsync`, `ReconfigureAsync`
>  and `DestroyAsync`) are just helpers that send such immediate commands.

### Through the DeviceHost

By calling [`IDeviceHost`](../Host/IDeviceHost.cs) `SendCommand` method, relying on the `IDevice.DeviceName` to route the command to
the target device:

```csharp
DeviceHostCommandResult SendCommand( IActivityMonitor monitor,
                                     BaseDeviceCommand command,
                                     bool checkControllerKey = true,
                                     CancellationToken token = default );
```

The [DeviceHostCommandResult](../Host/DeviceHostCommandResult.cs) captures the result of the command sending operation.

## Command handling: Its all about command Completion!

The `Device.DoHandleCommandAsync` SHOULD be overridden otherwise, since all commands should be handled, the default implementation
systematically throws a `NotSupportedException`.

```csharp
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor,
                                             BaseDeviceCommand command,
                                             CancellationToken token )
```

When `DoHandleCommandAsync` is called, the command has been validated:

- The `BaseDeviceCommand.CheckValidity` has been successfully executed.
- The `BaseDeviceCommand.DeviceName` matches this `IDevice.Name" (or Device.UnsafeSendCommand or Device.UnsafeSendCommandImmediate 
has been used). 
- The `BaseDeviceCommand.ControllerKey` is either null or match the current `IDevice.ControllerKey` (or an Unsafe send has been used).
- `BaseDeviceCommand.StoppedBehavior` is coherent with this current `IsRunning` state.

Typical `DoHandleCommandAsync` implementation applies pattern matching on the command type and handles
it the way its wants, either directly or through a totally desynchronized process:
the completion of a command **IS NOT** the completion of the `DoHandleCommandAsync`: the 
command handling **MUST** ensure that the `DeviceCommandNoResult.Completion` 
or `DeviceCommandWithResult<TResult>.Completion` is eventually resolved 
by calling `SetResult`, `SetCanceled` or `SetException` on the Completion otherwise the caller 
may indefinitely wait for the command completion.

## DeviceCommandStoppedBehavior

Each Command has an overridable `StoppedBehavior` that specifies how it should be handled when the device is stopped.
The [DeviceCommandStoppedBehavior](DeviceCommandStoppedBehavior.cs) enumeration describes th 8 available options.

