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

Specialized device's code use the `protected IDeviceLoop EventLoop { get; }` property
to safely communicate with the external world: 

```csharp
    /// <summary>
    /// Models the event loop API available inside an ActiveDevice.
    /// </summary>
    public interface IEventLoop : IMonitoredWorker
    {
        /// <summary>
        /// Sends a device event into <see cref="DeviceEvent"/>.
        /// </summary>
        /// <returns>The event.</returns>
        TEvent RaiseEvent( TEvent e );
    }
```

Just like the [CommandLoop](../Device/Device.ICommandLoop.cs), this extends the `CK.Core.IMonitoredWorker` interface
defined in CK.ActivityMonitor package: see Device's [CommandLoop and Signals](../Device/README.md#CommandLoop-and-Signals).

*Note:* The `EventLoop` property is protected. Often, it must be exposed to its whole assembly. To expose it simply use
the `new` masking operator:

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


