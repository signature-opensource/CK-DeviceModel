# CK-DeviceModel

[Devices](CK.DeviceModel/Device) are like micro hosted services with a name.
They can be running or not (Started/Stopped) and (re)configured dynamically.

Device implementation heavily relies on [Commands](CK.DeviceModel/Command). Their execution is isolated in an asynchronous loop.

All devices are configured (see [DeviceConfiguration](CK.DeviceModel/DeviceConfiguration.cs)) and can be reconfigured dynamically.
The configuration can specify that the device must be `AlwaysRunning` and a [daemon](CK.DeviceModel/Daemon) automatically
ensures that they are restarted when stopped (typically due to an unexpected error).

By installing the optional [CK.DeviceModel.Configuration](CK.DeviceModel.Configuration) package,
configurations from the standard .Net configuration API (*appsettings.json* and so on) are automatically and dynamically
applied from the **CK-DeviceModel** root configuration section.

See also:
[Device](CK.DeviceModel/Device),
[Command](CK.DeviceModel/Command),
[The 5 basic commands](CK.DeviceModel/Command/Basic), 
[ActiveDevice](CK.DeviceModel/ActiveDevice),
[DeviceHost](CK.DeviceModel/Host),
[Daemon](CK.DeviceModel/Daemon), 
[CK.DeviceModel.Configuration package](CK.DeviceModel.Configuration).

## Passive and Active devices

There are 2 kind of devices:
- the basic ones are passive: they handle commands and emit only events related to their lifetime (status,
configuration and controller key changes)
- the [active devices](ActiveDevice) extends basic ones and are able to emit specific events either in response to commands
or because the actual/physical device they interface is itself active.

## A simple passive device

Implementing a new device requires to define its host and its configuration types. Here is a complete implementation of a simple
passive device (a flash bulb):

- The host definition (that may offer a specific API if needed, but nothing more that this is required).

```csharp
public sealed class FlashBulbHost : DeviceHost<FlashBulb, DeviceHostConfiguration<FlashBulbConfiguration>, FlashBulbConfiguration>
{
}
```
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
```

- The device itself. This is a minimal definition without any Command. It holds the current color to use and 
  tracks whether its value is from the configuration.

```csharp
public sealed class FlashBulb : Device<FlashBulbConfiguration>
{
int _color;
bool _colorFromConfig;

public FlashBulb( IActivityMonitor monitor, CreateInfo info )
    : base( monitor, info )
{
    _color = info.Configuration.FlashColor;
    _colorFromConfig = true;
}

protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor,
                                                                      FlashBulbConfiguration config )
{
    bool colorChanged = config.FlashColor != CurrentConfiguration.FlashColor;
    bool configHasChanged = colorChanged || config.FlashRate != CurrentConfiguration.FlashRate;

    if( colorChanged && _colorFromConfig )
    {
        _color = config.FlashColor;
    }

    return Task.FromResult( configHasChanged
                                ? DeviceReconfiguredResult.UpdateSucceeded
                                : DeviceReconfiguredResult.None );
}

protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
{
    return Task.FromResult( true );
}

protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
{
    return Task.CompletedTask;
}

protected override Task DoDestroyAsync( IActivityMonitor monitor )
{
    return Task.CompletedTask;
}
}

```

- Defining two commands (one without result and one with a result).

```csharp
/// <summary>
/// This command triggers a flash on the bulb.
/// </summary>
public sealed class FlashCommand : DeviceCommand<FlashBulbHost>
{
}

/// <summary>
/// This command sets the color of the flash (or resets it to the configured
/// color) and returns the previous color.
/// </summary>
public sealed class SetFlashColorCommand : DeviceCommand<FlashBulbHost,int>
{
    /// <summary>
    /// The new color to set.
    /// Null to reset the color to the <see cref="FlashBulbConfiguration.FlashColor"/>.
    /// </summary>
    public int? Color { get; set; }
}
```

- The implementation of the two commands:

```csharp
protected override async Task DoHandleCommandAsync( IActivityMonitor monitor,
                                                    BaseDeviceCommand command,
                                                    CancellationToken token )
{
    switch( command )
    {
        case FlashCommand f:
            // ...Do whatever is needed here to make the bulb flash using
            // the current _color and CurrentConfiguration.FlashRate...
            f.Completion.SetResult();
            return;
        case SetFlashColorCommand c:
            {
                var prevColor = _color;
                if( c.Color != null )
                {
                    _color = c.Color.Value;
                    _colorFromConfig = false;
                }
                else
                {
                    _color = CurrentConfiguration.FlashColor;
                    _colorFromConfig = true;
                }
                c.Completion.SetResult( prevColor );
                return;
            }
    }
    // The base.DoHandleCommandAsync throws a NotSupportedException: all defined
    // commands MUST be handled above.
    await base.DoHandleCommandAsync( monitor, command, token ).ConfigureAwait( false );
}
```
>  More documentation on Commands can be found [here](CK.DeviceModel/Command#command-handling-its-all-about-command-completion).

- Finally, a simple helper that triggers a flash directly on the device: such specific device API must always be simple helpers that eventually send a command (an await its completion).

```csharp
public async Task<bool> FlashAsync( IActivityMonitor monitor )
{
    var cmd = new FlashCommand();
    if( !UnsafeSendCommand( monitor, cmd ) )
    {
        // The device has been destroyed.
        return false;
    }
    // Wait for the command to complete.
    await cmd.Completion.Task.ConfigureAwait( false );
    return true;
}
```

- To conclude, an example of the *appsettings.json* configuration file for such devices (this is automatically handled
whenever the [CK.DeviceModel.Configuration](CK.DeviceModel.Configuration) package is installed):

```jsonc
{
  // The root section must be CK-DeviceModel.
  "CK-DeviceModel": {
    // Hosts are found thanks to their name. 
    "FlashBulbHost": {
      // Optionally, hosts can have their own configuration if needed, but by default
      // only the Items (the devices) are handled.
      "Items": {
        // Devices are named objects.
        "FlashBulb n°1":
        {
          "Status": "AlwaysRunning",
          "FlashColor": 45,
          "FlashRate": 100
        },
        "FlashBulb n°2":
        {
          "Status": "RunnableStarted",
          "ControllerKey": "WebAPI"
          "FlashColor": 12,
        },
        "Another FlashBulb":
        {
          "Status": "Disabled",
          "FlashColor": 12,
        }
      }
    }
  }
}
```


