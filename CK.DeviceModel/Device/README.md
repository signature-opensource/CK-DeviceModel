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
that send their respective [5 basic commands](../Command/Basic)).

The Device implementation handles the nitty-gritty details of device life cycle and provide
any implementation for really safe asynchronous command handling and independent monitoring thanks to an
internal asynchronous loop.

Specialized devices must provide implementations for:

```csharp
protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );
protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );
protected abstract Task DoDestroyAsync( IActivityMonitor monitor );
```

## Commands

Devices can support any number of specific methods (devices can be seen as multiple instances of micro [IHostedService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostedservice)
that support dynamic reconfiguration), but their implementations SHOULD only be helpers that send Commands.

> Any device implementation MUST rely on Commands. This is the only way to prevent concurrency issues.

Key features of the Commands support are:

- Command execution is serialized thanks to [channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) 
and an internally managed asynchronous command loop with its own ActivityMonitor.
- Commands can generate a result (see [DeviceCommand&lt;TResult&gt;](../Command/DeviceCommandT.cs)) or not (see [DeviceCommand](../Command/DeviceCommand.cs).
- Commands that are handled while the device is stopped can be considered as errors, be canceled, be executed anyway or deferred until the device
 starts again (see the [DeviceCommandStoppedBehavior enumeration](../Command/DeviceCommandStoppedBehavior.cs).
- Commands can be sent immediately (highest priority) or delayed, waiting for their `SendingTimeUtc`.
- Commands completion MUST be signaled explicitly.
- Commands may transform errors or cancellation into command results. The [BaseReconfigureDeviceCommand](../Command/Basic/BaseConfigureDeviceCommand.cs)
is an example where errors or cancellation are mapped to [DeviceApplyConfigurationResult](../Host/DeviceApplyConfigurationResult.cs) enumeration values.
- Completed commands (even the ones that are completed outside of the command loop and regardless of their state - error, canceled or success) 
can be safely "continued" thanks to the Device's `OnCommandCompletedAsync` method.

More on Commands [here](../Command).

## Reminders

Devices support time delayed operations thanks to "Reminders". This is of course a protected API that can be called from
the Device's code only:

```csharp
/// <summary>
/// Registers a reminder that must be in the future or, by default, an <see cref="ArgumentException"/> is thrown.
/// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
/// eventually be called.
/// </summary>
/// <param name="timeUtc">The time in the future at which <see cref="OnReminderAsync"/> will be called.</param>
/// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
/// <param name="throwIfPast">False to returns false instead of throwing an ArgumentExcetion if the reminder cannot be registered.</param>
/// <returns>True on success or false if the reminder cannot be set and <paramref name="throwIfPast"/> is false.</returns>
protected bool AddReminder( DateTime timeUtc, object? state, bool throwIfPast = true );

/// <summary>
/// Reminder callback triggered by <see cref="AddReminder(DateTime, object?, bool)"/>.
/// This does nothing at this level.
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="reminderTimeUtc">The exact time configured on the reminder.</param>
/// <param name="state">The optional state.</param>
/// <returns>The awaitable.</returns>
protected virtual Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state );
```

Reminders can be added and are triggered regardless of the Device status (stopped or running).

## Configuration

Device are instantiated with an initial configuration and can be reconfigured at any time. [DeviceConfiguration](../DeviceConfiguration.cs)
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
a [DeviceConfigurationChangedEvent&lt;TConfiguration&gt;](LifetimeEvent/DeviceConfigurationChangedEvent.cs)
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

