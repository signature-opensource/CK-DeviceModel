# Commands

All device commands must inherit from [DeviceCommand](DeviceCommand&lt;THost&gt;.cs) or [DeviceCommand&lt;THost,TResult&gt;](DeviceCommandT.cs)
if the command generates a result. All commands ultimately specialize [BaseDeviceCommand](BaseDeviceCommand.cs).

## Command sending

Commands are sent to a device and instantly queued. They are executed asynchronously, in the order of the queue, by each
device's command loop: developers are isolated from any concurrency issues since all commands (including
the [5 basic commands](Basic)) are handled sequentially.

Commands are normally executed one after the others. However, sometimes a command should be handled as soon as possible,
without waiting for the current pending commands to be handled. Such commands only need to have their `ImmediateSending`
property sets to true.
   
This shortcuts the "regular" queue and there is still NO concurrency to handle, the "immediate commands" will be handled
immediately or right after the end of the currently executing command (if any).
*Immediate* commands are simply enqueued in a high priority queue.

Another internal queue exists: the queue of the deferred commands that contains the commands that cannot be handled
by a stopped device. These deferred commands are automatically executed as soon as a device restarts.

### On the Device

Commands can be sent to a device by using the 2 available methods directly on any [IDevice](../Device/IDevice.cs) object:

```csharp
bool SendCommand( IActivityMonitor monitor,
                  BaseDeviceCommand command,
                  bool checkDeviceName = true,
                  bool checkControllerKey = true,
                  CancellationToken token = default );

bool UnsafeSendCommand( IActivityMonitor monitor,
                        BaseDeviceCommand command,
                        CancellationToken token = default );
```
These methods return `false` if the device is destroyed and cannot receive commands anymore. They throw an `ArgumentException`
if the command is null or if its `CheckValidity` method returns `false`: command validity MUST be checked before sending it.

#### Safe vs. Unsafe

By default, the `BaseDeviceCommand.DeviceName` MUST match the device's name, and the `BaseDeviceCommand.ControllerKey` must match the device's
current `ControllerKey` (or the latter is null). This is checked when the command is sent
and may raise an `ArgumentException`.

The controller key is not checked when the command is sent but right before the command execution (this is because a previously
handled command can change the controller key). If the controller key doesn't match, an [InvalidControllerKeyException](../Device/InvalidControllerKeyException.cs)
is set on the command completion.

This (safe) behavior can be amended thanks to the SendCommand parameters or by calling the UnsafeSendCommand method.

### Through the DeviceHost

Commands can be sent by calling [`IDeviceHost`](../Host/IDeviceHost.cs) `SendCommand` method, relying on the `IDevice.DeviceName`
to route the command to the target device:

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
- The `BaseDeviceCommand.DeviceName` matches this `IDevice.Name" (or Device.UnsafeSendCommand has been used). 
- The `BaseDeviceCommand.ControllerKey` is either null or match the current `IDevice.ControllerKey` (or an Unsafe send has been used).
- `BaseDeviceCommand.StoppedBehavior` is coherent with this current `IsRunning` state.

Typical `DoHandleCommandAsync` implementation applies pattern matching on the command type and handles
it the way its wants, either directly or through a totally desynchronized process:
the completion of a command **IS NOT** the completion of the `DoHandleCommandAsync`: the 
command handling **MUST** ensure that the `DeviceCommandNoResult.Completion` 
or `DeviceCommandWithResult<TResult>.Completion` is eventually resolved 
by calling `SetResult`, `SetCanceled` or `SetException` on the Completion otherwise the caller 
may indefinitely wait for the command completion.

## StoppedBehavior and ImmediateStoppedBehavior

Each Command has an overridable `StoppedBehavior` and `ImmediateStoppedBehavior` that specify how it should be handled when the device is stopped.
The [DeviceCommandStoppedBehavior](DeviceCommandStoppedBehavior.cs) enumeration describes the 8 available options.

This "stopped behavior" is rather complete and should cover all needs. The default behavior is `WaitForNextStartWhenAlwaysRunningOrCancel`
that cancels the command (calling `SetCanceled` on the command's completion) if the device is stopped, unless the DeviceConfigurationStatus is
`AlwaysRunning`: in such case, the command is stored in an internal queue and executed as soon as the device restarts.

Another useful behavior is `RunAnyway`: all the basic commands (Destroy, Reconfigure, SetControllerKey, Start and Stop) uses this
behavior since they must obviously do their job even if the device is stopped (the Stop does nothing when the device is already stopped).

For "immediate" commands, [DeviceImmediateCommandStoppedBehavior](DeviceImmediateCommandStoppedBehavior.cs) there is only 3 options since
immediate commands cannot be deferred.
