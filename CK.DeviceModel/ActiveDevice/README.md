# ActiveDevice

An [IActiveDevice&lt;TEvent&gt;](IActiveDeviceT.cs) is a specialized [Device](../Device/Device.cs) that emits Device specific events
in addition to the standard [device lifetime events](../Device/LifetimeEvent/DeviceLifetimeEvent.cs).

There are 2 base classes for Active devices:

- The [SimpleActiveDevice](SimpleActiveDevice.cs) raises its events from the command loop.
- The [ActiveDevice](ActiveDevice.cs) runs an internal event dispatch loop with a dedicate monitor.

These base classes have 2 goals:
- Simplifying implementations of devices (like any instance of the *template method* pattern) that in
addition to handle commands must also emit events to the external world.
- Offer a standardized API on all possible devices.

> When specializing SimpleActiveDevice or ActiveDevice base classes, a specialized `TEvent : BaseActiveDeviceEvent` 
> that will be the common type signature of all the events of the device must be defined.
> Note that the base event class MUST be the generic [ActiveDeviceEvent](ActiveDeviceEvent.cs) but this has not been
> modeled to avoid a `TSelf` (or `TThis`) of the ["Curiously Recurring Pattern"](https://en.wikipedia.org/wiki/Curiously_recurring_template_pattern)).

## Unified event API

The [IActiveDevice](IActiveDevice.cs) is a non-generic interface that is shared by any active device. It exposes the `AllEvent`
that can listen to [LifetimeEvent](../Device/LifetimeEvent/DeviceLifetimeEvent.cs) (configuration, status and controller key changes)
as well as specialized [ActiveDeviceEvent](ActiveDeviceEvent.cs).

Any `IActiveDevice` also expose a low-level method:

```csharp
  /// <summary>
  /// Posts an event in this device's event queue.
  /// This should be used with care and can be used to mimic a running device.
  /// <para>
  /// Event's type must match the actual <see cref="ActiveDeviceEvent{TDevice}"/> type otherwise an <see cref="InvalidCastException"/> is thrown.
  /// </para>
  /// </summary>
  /// <param name="e">The event to inject.</param>
  void DebugPostEvent( BaseActiveDeviceEvent e );
```

Note that, by design, this is only able to emulate `ActiveDeviceEvent` (not `LifetimeEvent`).

Specific device events are accessible through the generic [IActiveDevice&lt;TEvent&gt;](IActiveDeviceT.cs) (specific events
are strongly typed). However, since `IActiveDevice.AllEvent` raises both device and lifetime events, non-generic event handling
can be done with the `AllEvent`.

## The ActiveDevice.IEventLoop interface

Device's code can use the `protected IDeviceLoop EventLoop { get; }` property
to safely communicate with the external world: 

```csharp
    /// <summary>
    /// Models the event loop API available inside an ActiveDevice.
    /// </summary>
    public interface IEventLoop : IActivityLogger
    {
        /// <summary>
        /// Sends an immediate signal into the event loop that will be handled by <see cref="ActiveDevice.OnEventSignalAsync(IActivityMonitor, object?)"/>.
        /// An <see cref="ArgumentException"/> is thrown if the <paramref name="payload"/> is a <see cref="BaseDeviceCommand"/>
        /// or <see cref="BaseDeviceEvent"/>.
        /// </summary>
        /// <param name="payload">The payload to send. It must not be a command nor an event.</param>
        void Signal( object? payload );

        /// <summary>
        /// Sends a device event into <see cref="DeviceEvent"/>.
        /// </summary>
        /// <returns>The event.</returns>
        TEvent RaiseEvent( TEvent e );

        /// <summary>
        /// Executes a lambda function on the event loop. This is dangerous because
        /// of the closure lambda: fields may be written concurrently.
        /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
        /// (a record class should typically be used) to better express the "command" pattern.
        /// </summary>
        /// <param name="action">The action that will be executed in the command loop context.</param>
        void DangerousExecute( Action<IActivityMonitor> action );

        /// <summary>
        /// Executes an asynchronous lambda function on the event loop. This is dangerous because
        /// of the closure lambda: fields may be written concurrently.
        /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
        /// (a record class should typically be used) to better express the "command" pattern.
        /// </summary>
        /// <param name="action">The asynchronous action that will be executed in the command loop context.</param>
        void DangerousExecute( Func<IActivityMonitor, Task> action );
    }
```
__Note:__ This interface is actually split into a non generic base and the generic one but this is an implementation detail.

Just like the [CommandLoop](../Device/Device.ICommandLoop.cs), this extends the `CK.Core.IActivityLogger` interface
defined in CK.ActivityMonitor package: this is described in more details in the [Device's CommandLoop and Signals](../Device/README.md#CommandLoop-and-Signals).

Correctly handling concurrency is hard. An `ActiveDevice` has 2 parallel activities: its command and event loops.
When developing a device, one must always be able to state in which loop the code is being executed. The following helpers
available on a device (the first one) and an active device (both of them) should be used, typically in `Debug.Assert`:

```csharp
/// <summary>
/// Gets whether the current activity is executing in the command loop.
/// </summary>
/// <param name="monitor">The monitor.</param>
/// <returns>True if the monitor is the command loop monitor, false otherwise.</returns>
protected bool IsInCommandLoop( IActivityMonitor monitor ) => monitor == _commandMonitor;

/// <summary>
/// Gets whether the current activity is executing in the event loop.
/// </summary>
/// <param name="monitor">The monitor.</param>
/// <returns>True if the monitor is the event loop monitor, false otherwise.</returns>
protected bool IsInEventLoop( IActivityMonitor monitor ) => monitor == _eventMonitor;
```

The key is the monitor! (This is the same idea that the `CK.Core.AsyncLock` uses to be able to handle lock recursion and
to detect bad locking usage.)

An example of this principle: when already working in the device loop, events can be raised directly, without a useless
dispatch through the event loop channel.
To secure this direct call, a monitor is required: if it's the one of the event loop the event is directly raised
otherwise `IEventLoop.RaiseEvent( TEvent e )` is called:

```csharp
/// <summary>
/// Raises a device event from inside the event loop if the monitor is the one of the
/// event loop, otherwise posts the event to the loop.
/// </summary>
/// <param name="monitor">The monitor.</param>
/// <param name="e">The event to send.</param>
/// <returns>The awaitable.</returns>
protected Task RaiseEventAsync( IActivityMonitor monitor, TEvent e )
{
    if( IsInEventLoop( monitor ) )
    {
        return DoRaiseEventAsync( e );
    }
    DoPost( e );
    return Task.CompletedTask;
}
```

*Note:* The `EventLoop` property is protected. Often, it must be exposed to its whole assembly. To expose it simply use
the `new` masking operator (avoid making it public!):

```csharp
  public sealed class SignatureDevice : ActiveDevice<SignatureDeviceConfiguration,SignatureDeviceEvent>
  {
    ... 
    internal new IEventLoop EventLoop => base.EventLoop;
    ...
  }  
```

> An ActiveDevice has 2 protected loops at its disposal: the `Device.CommandLoop` and the `ActiveDevice.EventLoop`. Care must 
> be taken to which one a job is sent: they execute concurrently!


### Why is there no `virtual bool OnDeviceEventRaising( TEvent e )`?

This seems to be a useful hook: before sending an event to the external world, this could enable
filtering the event or could impact device state in a centralized manner.

This extension point is not available and this is intended. If common actions should be taken
before sending an event, we consider that this code must be factorized on the emit side with
one (or more) centralized method(s). This will be more maintainable than having yet-another centralized
pattern matching that is likely to be overlooked.


