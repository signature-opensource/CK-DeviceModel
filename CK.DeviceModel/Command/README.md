# Commands

All commands ultimately specialize [BaseDeviceCommand](BaseDeviceCommand.cs).
All device commands must inherit from [DeviceCommand](DeviceCommand&lt;THost&gt;.cs) or [DeviceCommand&lt;THost,TResult&gt;](DeviceCommandT.cs)
if the command generates a result. 

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

Two other internal queues exist:
- the queue of the deferred commands that contains the commands that cannot be handled
by a stopped device. These deferred commands are automatically executed as soon as a device restarts.
- a PriorityQueue where commands that have a `SendingTimeUtc` in the future are stored.

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
These methods throw an `ArgumentException` if the command is null or if its `CheckValidity` method returns `false`:
command MUST be valid when they are sent.

These methods return `false` if the device is destroyed and cannot receive commands anymore: the command completion has been
called with an `UnavailableDeviceException` (that, depending on the command, may have been transformed into a canceled or successful
command task's result - see below).


#### Safe vs. Unsafe

By default, the `BaseDeviceCommand.DeviceName` MUST match the device's name, and the `BaseDeviceCommand.ControllerKey` must match the device's
current `ControllerKey` (or the latter is null). This is checked when the command is sent
and may raise an `ArgumentException`.

The controller key is not checked when the command is sent but right before the command execution (this is because a previously
handled command can change the controller key). If the controller key doesn't match, an [InvalidControllerKeyException](../Device/InvalidControllerKeyException.cs)
is set on the command completion.

This (safe) behavior can be amended thanks to the `SendCommand` parameters or by calling the `UnsafeSendCommand` method.

### Through the DeviceHost

Commands can be sent by calling [`IDeviceHost`](../Host/IDeviceHost.cs) `SendCommand` method, relying on the `IDevice.DeviceName`
to route the command to the target device:

```csharp
DeviceHostCommandResult SendCommand( IActivityMonitor monitor,
                                     BaseDeviceCommand command,
                                     bool checkControllerKey = true,
                                     CancellationToken token = default );
```

The [DeviceHostCommandResult](../Host/DeviceHostCommandResult.cs) captures the result of the command sending operation
through a host.

## Delayed commands

Actual command sending can be delayed thanks to:

```csharp
/// <summary>
/// Gets or sets the sending time of this command.
/// When null (the default) or set to <see cref="Util.UtcMinValue"/> the command is executed
/// (as usual) when it is dequeued.
/// <para>
/// When <see cref="ImmediateSending"/> is set to true, this SendingTimeUtc is automatically set to null.
/// And when this is set to a non null UTC time, the ImmediateSending is automatically set to false.
/// </para>
/// <para>
/// The value should be in the future but no check is done against <see cref="DateTime.UtcNow"/>
/// in order to safely handle any clock drift: if the time is in the past when the command is dequeued,
/// it will be executed like any regular (non immediate) command.
/// </para>
/// </summary>
public DateTime? SendingTimeUtc { get; set; }
```

As the comment states, `SendingTimeUtc` and `ImmediateSending` are exclusive.

## Command handling: Its all about command Completion!

The `Device.DoHandleCommandAsync` SHOULD be overridden otherwise, since all commands should be handled, the default implementation
systematically throws a `NotSupportedException`.

```csharp
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor,
                                             BaseDeviceCommand command )
```

When `DoHandleCommandAsync` is called, the command has been validated:

- The `BaseDeviceCommand.CheckValidity` has been successfully executed.
- The `BaseDeviceCommand.DeviceName` matches this `IDevice.Name" (or Device.UnsafeSendCommand has been used). 
- The `BaseDeviceCommand.ControllerKey` is either null or match the current `IDevice.ControllerKey` (or an Unsafe send has been used).
- `BaseDeviceCommand.StoppedBehavior` is coherent with this current `IsRunning` state.
- The `BaseDeviceCommand.SendingTimeUtc` is in the now (or in the past).
- `GetCommandTimeoutAsync` has been called and the command's timeout is configured.

Typical `DoHandleCommandAsync` implementation applies pattern matching on the command type and handles
it the way its wants, either directly or through a totally desynchronized process:
the completion of a command **IS NOT** the completion of the `DoHandleCommandAsync`: the 
command handling **MUST** ensure that the `DeviceCommandNoResult.Completion` 
or `DeviceCommandWithResult<TResult>.Completion` is eventually resolved 
by calling `SetResult`, `SetCanceled` or `SetException` on the Completion otherwise the caller 
may indefinitely wait for the command completion.
When `DoHandleCommandAsync` doesn't complete the command, the command is "Long Running" (more on this below).

`DoHandleCommandAsync` has no CancellationToken parameter: the command exposes a unique token that unifies the
multiple ways to cancel a command: `DoHandleCommandAsync` have just to use this unique token when calling
any external asynchronous methods (more on this below).

## Error or Cancellation as Command results
Error management is never simple. Consider the Destroy command for instance: can it fail? Actually not:
- First, when destroying a device, any error that occurred must not prevent the device to be destroyed.
- Second, this is an idempotent action: regardless of any race condition or concurrency issues, destroying an already destroyed
device is fine.

Of course, this doesn't prevent a buggy device to hang forever (there is currently no timeout on the destroy) or to leave opened
resources (handles, pipes, etc.), but the developer that uses a device has almost none possibilities to handle these
bugs in the device.

Commands and their Completion offer a solid way to handle this scenario: Commands can hook the error and/or canceled case and
transform their task's result according to their semantics. The destroy command for instance does just that
(in [BaseDestroyDeviceCommand](Basic/BaseDestroyDeviceCommand.cs)):

```csharp
protected override void OnError( Exception ex, ref CompletionSource.OnError result ) => result.SetResult();

protected override void OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetResult();
```

The configuration command is even more interesting since this command has a result that can express the error or canceled issues
(in [BaseConfigureDeviceCommand](Basic/BaseConfigureDeviceCommand.cs)):

```csharp
protected override void OnCanceled( ref CompletionSource<DeviceApplyConfigurationResult>.OnCanceled result )
{
    result.SetResult( DeviceApplyConfigurationResult.ConfigurationCanceled );
}

protected override void OnError( Exception ex, ref CompletionSource<DeviceApplyConfigurationResult>.OnError result )
{
    if( ex is InvalidControllerKeyException ) result.SetResult( DeviceApplyConfigurationResult.InvalidControllerKey );
    else result.SetResult( DeviceApplyConfigurationResult.UnexpectedError );
}
```

If it is needed, the original exception or the fact that the command has actually been canceled is available on the `ICompletion` (that comes from
CK.Core assembly).

## StoppedBehavior and ImmediateStoppedBehavior

Each Command has an overridable `StoppedBehavior` and `ImmediateStoppedBehavior` that specify how it should be handled when the device is stopped.
The [DeviceCommandStoppedBehavior](DeviceCommandStoppedBehavior.cs) enumeration describes the 8 available options.

This "stopped behavior" is rather complete and should cover all needs. The default behavior is `WaitForNextStartWhenAlwaysRunningOrCancel`
that cancels the command (calling `SetCanceled` on the command's completion) if the device is stopped, unless the DeviceConfigurationStatus is
`AlwaysRunning`: in such case, the command is stored in an internal queue and executed as soon as the device restarts.

Another useful behavior is `RunAnyway`: all the basic commands (Destroy, Reconfigure, SetControllerKey, Start and Stop) uses this
behavior since they must obviously do their job even if the device is stopped (the Stop does nothing when the device is already stopped).

For "immediate" commands, [DeviceImmediateCommandStoppedBehavior](DeviceImmediateCommandStoppedBehavior.cs) has only 3 options since
immediate commands cannot be deferred.

## Cancellations & timeout

Many reasons can lead to the cancellation of a command. A "reason" string is exposed on the Command that describes
why the command has been canceled:

```csharp
/// <summary>
/// Gets the cancellation reason if a cancellation occurred.
/// </summary>
public string? CancellationReason { get; }
```

### Multiple cancellation sources

First, any number of CancellationToken can be enlisted at any time on a Command thanks to:
```csharp
/// <summary>
/// Registers a source for this <see cref="CancellationToken"/> along with a reason.
/// Nothing is done if <see cref="CancellationToken.CanBeCanceled"/> is false
/// or this command has already been completed (see <see cref="ICompletion.IsCompleted"/>).
/// <para>
/// Whenever one of the added token is canceled, <see cref="ICompletionSource.TrySetCanceled()"/> is called.
/// If the token is already canceled, the call to try to cancel the completion is made immediately.
/// </para>
/// </summary>
/// <param name="t">The token.</param>
/// <param name="reason">
/// Reason that will be <see cref="CancellationReason"/> if this token is the first to cancel the command.
/// This must not be empty or whitespace nor <see cref="CommandCompletionCanceledReason"/>, <see cref="CommandTimeoutReason"/> or <see cref="SendCommandTokenReason"/>.
/// </param>
/// <returns>True if the token has been registered or triggered the cancellation, false otherwise.</returns>
public bool AddCancellationSource( CancellationToken t, string reason )
```
Host's and Device's `SendCommand` methods enlist their optional `CancellationToken` parameter with
the `SendCommandToken` reason string.

Second, a Command can be explicitly canceled in two ways:
- By calling its `Completion.TrySetCancel()` (or `Completion.SetCancel()`) method. In this
case the reason is the constant string `CommandCompletionCanceled`.
- By calling the Command's `void Cancel( string reason )` method that enables to set an explicit reason.

Last but not least, a Command can have an associated timeout in milliseconds. This timeout can be estimated/computed
by the `Device.GetCommandTimeoutAsync` method:
```csharp
/// <summary>
/// Called right before <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/> to compute a
/// timeout in milliseconds for the command that is about to be executed.
/// By default this returns 0: negative or 0 means that no timeout will be set on the command.
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="command">The command about to be handled by <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/>.</param>
/// <returns>A timeout in milliseconds. 0 or negative if timeout cannot or shouldn't be set.</returns>
protected virtual ValueTask<int> GetCommandTimeoutAsync( IActivityMonitor monitor, BaseDeviceCommand command ) => new ValueTask<int>( 0 ); 
```
When positive, this timeout may cancel the command with the `CommandTimout` reason string.

### The command's CancellationToken

When handling a command, a unique token is exposed by the Command that summarizes all cancellations:
```csharp
/// <summary>
/// Gets a cancellation token that combines all tokens added by <see cref="AddCancellationSource(CancellationToken, string)"/>,
/// command timeout, and cancellations on the Completion or via <see cref="Cancel(string)"/>.
/// It must be used to cancel any operation related to the command execution. 
/// </summary>
public CancellationToken CancellationToken { get; }
```


## OnCommandCompletedAsync: command continuations

When a command has completed (whatever its final state is), continuations can easily occur thanks to the Device's virtual method:
```csharp
/// <summary>
/// Called when a command completes and its <see cref="BaseDeviceCommand.ShouldCallDeviceOnCommandCompleted"/> is true.
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="command">The completed command.</param>
/// <returns>The awaitable.</returns>
protected virtual Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command ) => Task.CompletedTask;
```

Whether this method is called or not depends on the public `BaseDeviceCommand.ShouldCallDeviceOnCommandCompleted` boolean
command's property that is:
- False for the 5 standard commands.
- True by default for any other commands. It can be changed at any time (before the completion occurs of course).

## Long Running commands

> Before waiting for a command completion, one may want to know whether the time to wait can be way tooooo loooong!

As said before, the completion of a command **IS NOT** the completion of the `DoHandleCommandAsync` method.
A command can perfectly be completed before (typically through cancellation) or after its handling (the command is waiting
for an external event for instance): when the handler does not complete the command it is considered as Long Running.

Regardless of its handling, a delayed (via its `SendingTimeUtc`) or deferred (waiting for the device to be restarted) command
will for sure take some time to be completed: delayed or deferred commands are also considered as Long Running.

And just like cancellations (see above), there may be a lot of reason for a command to be Long Running (it has to wait for
an incoming mail for instance): a reason string can be explicitly set on a Command (as long as it has not been already set).

### Long Running API
The Device commands expose a rather simple public API to support this:

```csharp
public Task<string?> LongRunningReason { get; }

/// <summary>
/// Gets whether this command is known to be long running or not.
/// This is null until this is known.
/// </summary>
public bool? IsLongRunning { get; }

/// <summary>
/// Atomically tries to set the <see cref="LongRunningReason"/>.
/// </summary>
/// <param name="reason">The reason to set.</param>
/// <returns>True on success, false if a reason was previously set.</returns>
public bool TrySetLongRunningReason( string? reason );
```

The `LongRunningReason` is null when the command is known to be short running: it has already been handled or
should be handled soon.

The reason can be any string if `TrySetLongRunningReason` is used. Internally the 3 constants "Deferred", "Delayed"
and "WaitForCompletion" are used.

On the device implementation side, the protected `OnLongRunningCommandAppearedAsync` can be overridden to implement
controls or tracking of the commands that happened to be Long Running:

```csharp
/// <summary>
/// Called whenever a command is known to be long running.
/// See <see cref="BaseDeviceCommand.LongRunningReason"/>.
/// <para>
/// This method does nothing at this level.
/// </para>
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="command">The long running command.</param>
/// <returns>The awaitable.</returns>
protected virtual ValueTask OnLongRunningCommandAppearedAsync( IActivityMonitor monitor, BaseDeviceCommand command ) => default;
```

### Why should I care?

You may simply ignore this capability and use the "Always Waiting for Completion" approach. Providing that the
cancellations and/or command timeout is properly used, this is fine.

However some scenario require more control. A Web API for instance can not wait indefinitely for the Completion
to happen: the initial request must be answered (the sooner, the better) typically with a command identifier
and a continuation mechanism must be stated (thanks to a polling or back channel).

One way to handle this (that is always possible) is to wait for the completion during a given delay (by using
https://github.com/Invenietis/CK-Core/blob/develop/CK.Core/Extension/TaskExtensions.cs#L29) but it would then
be more efficient to use the "Long Running" capability:

- Send the command.
- Await the `LongRunningReason`
  - If the reason is null, await the Completion and returns the result.
  - Otherwise:
    -  Choose a unique identifier for the command (you may use a [FastUniqueIdGenerator](https://github.com/Invenietis/CK-Core/blob/develop/CK.Core/FastUniqueIdGenerator.cs). 
    -  Enlist the Long Running Command (and an expiration time) in a (concurrent) dictionary indexed by its identifier.
    -  Expose this dictionary to polling methods and/or initiate a continuation on the Command's completion to trigger a call
       back to the initiator if it's possible.

This doesn't suppress the need to correctly manage the command timeout. And to fully secure the process, awaiting the `LongRunningReason`
can be done with a timeout.

### Why a Device doesn't track Long Running commands?

The `OnLongRunningCommandAppearedAsync` can be used to track the Long Running commands. This can be done easily by adding them
to an HashSet of commands (and deciding how and when they should be removed). But for what benefits? With which features?

Properly handling such commands heavily depends on the caller's expectations and not all the devices need a dashboard
of long running commands.

The recommended answer to this is to implement this externally and then, may be, to consider its inclusion.

