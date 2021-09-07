# ActiveDevice

An [ActiveDevice](ActiveDevice.cs) is a specialized [Device](../Device/Device.cs) that implements
an event dispatch loop in addition to the command loop of any device.

This base class has 2 goals:
- Simplifying implementations (like any instance of the *template method* pattern) of devices that in
addition to handle commands must also emit events to the external world.
- Offer a standardized API on all devices.
- 
> When specializing the [ActiveDevice](ActiveDevice.cs) base class, a specialized `TEvent : BaseActiveDeviceEvent` 
> that will be the common type signature of all the events of this device.
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

Note that this is only able to emulate `ActiveDeviceEvent` (not `LifetimeEvent`).

## The IEventLoop interface

The specialized device's code use the `protected IDeviceLoop DeviceLoop { get; }` property
to safely communicate with the external world: 

```csharp
    /// <summary>
    /// Models the event loop API available inside an ActiveDevice.
    /// </summary>
    protected interface IEventLoop
    {
        /// <summary>
        /// Sends a device event into <see cref="DeviceEvent"/>.
        /// </summary>
        /// <param name="e">The event to send.</param>
        void RaiseEvent( TEvent e );

        /// <summary>
        /// Posts the given synchronous action to be executed on the event loop.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        void Execute( Action<IActivityMonitor> action );

        /// <summary>
        /// Posts the given asynchronous action to be executed on the event loop.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        void Execute( Func<IActivityMonitor, Task> action );

        /// <summary>
        /// Posts an error log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogError( string msg );

        /// <summary>
        /// Posts an error log message with an exception into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        void LogError( string msg, Exception ex );

        /// <summary>
        /// Posts a warning log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogWarn( string msg );

        /// <summary>
        /// Posts a warning log message with an exception into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        void LogWarn( string msg, Exception ex );

        /// <summary>
        /// Posts an informational message log into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogInfo( string msg );

        /// <summary>
        /// Posts a trace log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogTrace( string msg );

        /// <summary>
        /// Posts a debug log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        void LogDebug( string msg );
    }
```

Thanks to the `void Execute( Action<IActivityMonitor> action )` method, any actions can be posted
to be executed in the context of the event loop: any visible side-effect of a running device should go
through this loop.


