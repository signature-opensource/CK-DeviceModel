# Device

Devices are configurable objects identified by a unique name inside a [device host](../Host) that support a command pattern to isolate the
device's running code and prevent concurrency issues.

## Device
A device basically exposes its [IDevice](IDevice.cs) interface that exposes:
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
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
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
- Commands completion MUST be signaled explicitly.
- Commands may transform errors or cancellation into command results. The [BaseReconfigureDeviceCommand](../Command/Basic/BaseConfigureDeviceCommand.cs)
is an example where errors or cancellation are mapped to [DeviceApplyConfigurationResult](../Host/DeviceApplyConfigurationResult.cs) enumeration values.
- Commands that are handled while the device is stopped can be considered as errors, be canceled, be executed anyway or deferred until the device
 starts again (see the [DeviceCommandStoppedBehavior enumeration](../Command/DeviceCommandStoppedBehavior.cs).

More on Commands [here](../Command).

## Configuration

Device are instantiated with an initial configuration and can be reconfigured at any time. [DeviceConfiguration](../DeviceConfiguration.cs)
are basic, Poco-like, mutable objects that must be binary serializable. Thanks to the binary serialization, Configuration can be
deep cloned: we use this to isolate the actual running configuration (that can be accessed through the protected `CurrentConfiguration`
property from device's code) and the device's publicly exposed `ExternalConfiguration` that is an independent clone.

Any device must define a concrete DeviceConfiguration specialization. A typical configuration looks like the following:
````
public class FlashBulbConfiguration : DeviceConfiguration
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
lifetime event is raised.

## Runtime properties

Nothing prevents a device to expose properties but we don't recommend it.

In such case, maximal care should be taken to handle concurrency. A good approach
is to expose only read-only properties and only update them through Commands (or as a side effect of
one or more handled commands).

