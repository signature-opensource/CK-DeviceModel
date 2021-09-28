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

The specialized device's code use the `protected IDeviceLoop EventLoop { get; }` property
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
        /// <param name="e">The event to send.</param>
        void RaiseEvent( TEvent e );
    }
```

This extends the `CK.Core.IMonitoredWorker` interface defined in CK.ActivityMonitor package:

```csharp
    /// <summary>
    /// Simple abstraction of a worker with its own monitor that
    /// can execute synchronous or asynchronous actions and offers
    /// simple log methods (more complex logging can be done via
    /// the <see cref="Execute(Action{IActivityMonitor})"/> method).
    /// <para>
    /// Note that the simple log methods here don't open/close groups and this
    /// is normal: the worker is free to interleave any workload between consecutive
    /// calls from this interface: structured groups have little chance to really be
    /// structured.
    /// </para>
    /// </summary>
    public interface IMonitoredWorker
    {
        /// <summary>
        /// Posts the given synchronous action to be executed by this worker.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        void Execute( Action<IActivityMonitor> action );

        /// <summary>
        /// Posts the given asynchronous action to be executed by this worker.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        void Execute( Func<IActivityMonitor, Task> action );

        /// <summary>
        /// Posts an error log message into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogError( string msg );

        /// <summary>
        /// Posts an error log message with an exception into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        void LogError( string msg, Exception ex );

        /// <summary>
        /// Posts a warning log message into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogWarn( string msg );

        /// <summary>
        /// Posts a warning log message with an exception into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        void LogWarn( string msg, Exception ex );

        /// <summary>
        /// Posts an informational message log into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogInfo( string msg );

        /// <summary>
        /// Posts a trace log message into this worker monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogTrace( string msg );

        /// <summary>
        /// Posts a debug log message this worker event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogDebug( string msg );
    }
```

Thanks to the `void Execute( Action<IActivityMonitor> action )` and `void Execute( Func<IActivityMonitor, Task> action )`
methods, any actions (synchronous as well as asynchronous) can be posted to be executed in the context of the event loop:
any visible side-effect of a running device should go through this loop.


