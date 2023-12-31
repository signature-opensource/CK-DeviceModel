# Device

Devices are configurable objects identified by a unique name inside a [device host](../Host) that support a command pattern to isolate the
device's running code and prevent concurrency issues.

## Device
A device basically exposes its [IDevice](IDevice.cs) interface:
- its stable unique device's Name inside its host and FullName (that is "DeviceHostName/Name"). 
- its changing lifetime status as a ([DeviceStatus](DeviceStatus.cs) as well as `IsRunning` and `IsDestroyed` easy to use booleans.
- its current `ControllerKey`.
- a clone of its current [configuration](../DeviceConfiguration.cs) named `ExternalConfiguration` to emphasize the fact that this 
NOT the actual configuration object that the devices is using.
- an event that emits [DeviceLifetimeEvent](LifetimeEvent/DeviceLifetimeEvent.cs).

And some methods:

- The fundamental (synchronous) `SendCommand`.
- 5 asynchronous helpers: `StartAsync`, `StopAsync`, `ReconfigureAsync`, `SetControllerKeyAsync` and `DestroyAsync`
that send their respective [5 basic commands](../Command/Basic/README.md)).

The Device implementation handles the nitty-gritty details of device life cycle and provide
any implementation for really safe asynchronous command handling (including delayed executions and cancellations) and
independent monitoring thanks to an internal asynchronous loop.

Specialized devices must provide implementations for:

```csharp
protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );
protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );
protected abstract Task DoDestroyAsync( IActivityMonitor monitor );
```
Optional extension points of a Device are:
```csharp
protected virtual DeviceCommandStoppedBehavior OnStoppedDeviceCommand( IActivityMonitor monitor, BaseDeviceCommand command );
protected virtual DeviceImmediateCommandStoppedBehavior OnStoppedDeviceImmediateCommand( IActivityMonitor monitor, BaseDeviceCommand command );
protected virtual Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling );
protected virtual ValueTask<bool> OnUnhandledExceptionAsync( IActivityMonitor monitor, BaseDeviceCommand command, Exception ex );
protected virtual ValueTask<int> GetCommandTimeoutAsync( IActivityMonitor monitor, BaseDeviceCommand command );
protected virtual Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command );
protected virtual ValueTask OnLongRunningCommandAppearedAsync( IActivityMonitor monitor, BaseDeviceCommand command );
protected virtual Task OnCommandSignalAsync( IActivityMonitor monitor, object? payload );
```

Details about Command handling can be found [here](../Command/README.md).

## CommandLoop and Signals

Device model's primary goal is to isolate developers from concurrency issues. This is done by an asynchronous loop
that dequeues the commands from a [Channel](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels)
(actually 2 and also from regular queues but this is an implementation detail).
Commands are handled sequentially one after the others: a device's state is never accessed concurrently as long as the
execution comes from one the device's method.

For real devices that interacts with the external world, typically listening to external events or communication channels,
the origin of the code is not the device itself. Any external code must use the `CommandLoop` to "transfer" its work to the
safe world of the loop.

```c#
/// <summary>
/// Gets the command loop API that implementation can use to execute 
/// actions, sends logs to the command loop or calls <see cref="ICommandLoop.Signal(object?)"/>.
/// </summary>
protected ICommandLoop CommandLoop => _commandLoop;
```

The `ICommandLoop` extends the [`CK.Core.IActivityLogger`](https://github.com/Invenietis/CK-ActivityMonitor/blob/develop/CK.ActivityMonitor/IActivityLogger.cs)
interface defined in CK.ActivityMonitor package: the command loop is a logger, all `Debug`, `Trace`, etc. logging methods are available
and are thread safe since the actual logging is made from the command loop on the command monitor (see
the [`ThreadsafeLogger`](https://github.com/Invenietis/CK-ActivityMonitor/blob/develop/Tests/CK.ActivityMonitor.Tests/DataPool/ThreadSafeLogger.cs)
sample to see how this is implemented).

Thanks to the `void DangerousExecute( Action<IActivityMonitor> action )` and `void DangerousExecute( Func<IActivityMonitor, Task> action )`
methods, any actions (synchronous as well as asynchronous) can be posted to be executed in the context of the command loop:
any process that interacts with the state of a device should go through this loop but it is safer to use the `Signal` method.

The `ICommandLoop` adds a notion of "signal" that avoids dangerous lambda functions and enables better code centralization:
because lambda functions are written at the call site, they can be really misleading for the reader/maintainer regarding
the possibly concurrent impact on the state:
```c#
/// <summary>
/// The command loop exposed by <see cref="CommandLoop"/>.
/// </summary>
protected interface ICommandLoop : IMonitoredWorker
{
    /// <summary>
    /// Sends an immediate signal into the command loop that will be handled by <see cref="OnCommandSignalAsync(IActivityMonitor, object?)"/>.
    /// An <see cref="ArgumentException"/> is thrown if the <paramref name="payload"/> is a <see cref="BaseDeviceCommand"/>.
    /// </summary>
    /// <param name="payload">The payload to send. Must not be a command.</param>
    void Signal( object? payload );

    /// <summary>
    /// Executes a lambda function on the command loop. This is dangerous because
    /// of the closure lambda: fields may be written concurrently.
    /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
    /// (a record class should typically be used) to better express the "command" pattern.
    /// </summary>
    /// <param name="action">The action that will be executed in the command loop context.</param>
    void DangerousExecute( Action<IActivityMonitor> action );

    /// <summary>
    /// Executes an asynchronous lambda function on the command loop. This is dangerous because
    /// of the closure lambda: fields may be written concurrently.
    /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
    /// (a record class should typically be used) to better express the "command" pattern.
    /// </summary>
    /// <param name="action">The asynchronous action that will be executed in the command loop context.</param>
    void DangerousExecute( Func<IActivityMonitor, Task> action );
}

```
Any object (except commands) can be sent as a "signal" (including null!). And this "signal" can be gently handled by a
dedicated handler (that should of course be overridden):

```c#
  /// <summary>
  /// Optional extension point that must handle <see cref="ICommandLoop.Signal(object?)"/> payloads.
  /// This does nothing at this level.
  /// <para>
  /// Any exceptions raised by this method will stop the device.
  /// </para>
  /// </summary>
  /// <param name="monitor">The monitor to use.</param>
  /// <param name="payload">The signal payload.</param>
  /// <returns>The awaitable.</returns>
  protected virtual Task OnCommandSignalAsync( IActivityMonitor monitor, object? payload ) => Task.CompletedTask;
```

The Signal/OnCommandSignalAsync is a simple helper that is a kind of "internal immediate multi-purpose" command. The paylaod
is totally internal: a simple set of strings like `"Disconnected"` or `"Connected"` can do the job, as well as internal static
singletons `internal static readonly object SignalConnected = new object();` (the latter is slightly more efficient since pure
reference equality can be used).

*Note:* The `CommandLoop` property is protected. Often, it must be exposed to the whole device's assembly.
To expose it simply use the `new` masking operator:

```csharp
  public sealed class MyDevice : Device<MyDeviceConfiguration>
  {
    ... 
    internal new ICommandLoop CommandLoop => base.CommandLoop;
    ...
  }  
```

> An ActiveDevice adds a similar (and concurrent) [`EventLoop`](../ActiveDevice/README.md#the-activedeviceieventloop-interface).

## Reminders

Devices support time delayed operations thanks to "Reminders". This is of course a protected API that can be called from
the Device's code only:

```csharp
/// <summary>
/// Registers a reminder in the delayed or immediate queue if the delay is too short to be delayed.
/// The <paramref name="timeUtc"/> can be in the past (an immediate queuing will be done), but not
/// in more than approximately 49 days (a <see cref="NotSupportedException"/> will be raised).
/// <para>
/// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
/// eventually be called even if the device is stopped.
/// </para>
/// </summary>
/// <param name="timeUtc">The time at which <see cref="OnReminderAsync"/> will be called.</param>
/// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
protected void AddReminder( DateTime timeUtc, object? state )

/// <summary>
/// Reminder callback triggered by <see cref="AddReminder(DateTime, object?)"/>.
/// This does nothing at this level.
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="reminderTimeUtc">The exact time configured on the reminder.</param>
/// <param name="state">The optional state.</param>
/// <param name="immediateHandling">True if the reminder has not been delayed but posted to the immediate queue.</param>
/// <returns>The awaitable.</returns>
protected virtual Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling );
```

Reminders can be added and are triggered regardless of the Device status (stopped or running).

## Configuration

Devices are instantiated with an initial configuration and can be reconfigured at any time. [DeviceConfiguration](../DeviceConfiguration.cs)
are basic, Poco-like, mutable objects that must be binary serializable. Thanks to the binary serialization, Configuration can be
deep cloned: we use this to isolate the actual running configuration (that can be accessed through the protected `CurrentConfiguration`
property from device's code) and the device's publicly exposed `ExternalConfiguration` that is an independent clone.

Any device must define a concrete DeviceConfiguration specialization:
- It must be in the same namespace and assembly as the Device.
- Its name must end with `Configuration`. 
- Default constructor and binary serialization support are required.
- The `DoCheckValid` method is optional but highly recommended since this avoid dealing with buggy configurations
in the `DoReconfigureAsync` method (and centralizes potentially complex code).

A typical configuration looks like the following:

- The device's configuration: its name MUST end with `Configuration` (and be in the same namespace and assembly). 
The `DoCheckValid` method is optional but default constructor and binary serialization support are required:

```csharp
public sealed class FlashBulbConfiguration : DeviceConfiguration
{
    /// <summary>
    /// A default public constructor is required.
    /// </summary>
    public FlashBulbConfiguration()
    {
    }

    public int FlashColor { get; set; }

    public int FlashRate { get; set; } = 1;

    protected override bool DoCheckValid( IActivityMonitor monitor )
    {
        bool isValid = true;
        if( FlashColor < 0 || FlashColor > 3712 )
        {
            monitor.Error( $"FlashColor must be between 0 and 3712." );
            isValid = false;
        }
        if( FlashRate <= 0 )
        {
            monitor.Error( $"FlashRate must be positive." );
            isValid = false;
        }
        return isValid;
    }

    /// <summary>
    /// Deserialization constructor.
    /// Every specialized configuration MUST define its own deserialization
    /// constructor (that must call its base) and override the <see cref="Write(ICKBinaryWriter)"/>
    /// method (that must start to call its base Write method).
    /// </summary>
    /// <param name="r">The reader.</param>
    public FlashBulbConfiguration( ICKBinaryReader r )
        : base( r )
    {
        r.ReadByte(); // version
        FlashColor = r.ReadInt32();
        FlashRate = r.ReadInt32();
    }

    /// <summary>
    /// Symmetric of the deserialization constructor.
    /// Every Write MUST call base.Write and write a version number.
    /// </summary>
    /// <param name="w">The writer.</param>
    public override void Write( ICKBinaryWriter w )
    {
        base.Write( w );
        w.Write( (byte)0 );
        w.Write( FlashColor );
        w.Write( FlashRate );
    }
}
````

Whenever a device's configuration changes,
a [DeviceLifetimeEvent&lt;TConfiguration&gt;](LifetimeEvent/DeviceLifetimeEventT.cs)
lifetime event is raised (this is a typed event: its base class is non-generic and expose the base `DeviceConfiguration`).

It is up to the device to accept or reject a new configuration:
```csharp
  /// <summary>
  /// Defines a subset of <see cref="DeviceApplyConfigurationResult"/> valid for a device reconfiguration:
  /// see <see cref="Device{TConfiguration}.DoReconfigureAsync(IActivityMonitor, TConfiguration)"/>.
  /// </summary>
  public enum DeviceReconfiguredResult
  {
      /// <summary>
      /// No reconfiguration happened.
      /// </summary>
      None = DeviceApplyConfigurationResult.None,

      /// <summary>
      /// The reconfiguration is successful.
      /// </summary>
      UpdateSucceeded = DeviceApplyConfigurationResult.UpdateSucceeded,

      /// <summary>
      /// The reconfiguration failed.
      /// </summary>
      UpdateFailed = DeviceApplyConfigurationResult.UpdateFailed,

      /// <summary>
      /// The updated configuration cannot be applied while the device is running.
      /// </summary>
      UpdateFailedRestartRequired = DeviceApplyConfigurationResult.UpdateFailedRestartRequired
  }

  /// <summary>
  /// Reconfigures this device. This can be called when this device is started (<see cref="IsRunning"/> can be true) and
  /// if reconfiguration while running is not possible or supported, <see cref="DeviceReconfiguredResult.UpdateFailedRestartRequired"/>
  /// should be returned.
  /// <para>
  /// It is perfectly valid for this method to return <see cref="DeviceReconfiguredResult.None"/> if nothing happened instead of
  /// <see cref="DeviceReconfiguredResult.UpdateSucceeded"/>. When None is returned, we may avoid a useless raise of the
  /// <see cref="DeviceConfigurationChangedEvent"/>.
  /// </para>
  /// </summary>
  /// <param name="monitor">The monitor to use.</param>
  /// <param name="config">The configuration to apply.</param>
  /// <returns>The reconfiguration result.</returns>
  protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
````

## Runtime properties

Nothing prevents a device to expose properties but we don't recommend it.

In such case, maximal care should be taken to handle concurrency. A good approach
is to expose only read-only properties and only update them through Commands (or as a side effect of
one or more handled commands).

