# CK-DeviceModel

[Devices](CK.DeviceModel/Device) are like micro hosted services with a name.
They can be running or not (Started/Stopped) and configured dynamically.

Device implementation heavily relies on [Commands](CK.DeviceModel/Command). Their execution is isolated in an asynchronous loop.

All devices are configured (see [DeviceConfiguration](CK.DeviceModel/DeviceConfiguration.cs)) and can be reconfigured dynamically.
The configuration can specify that the device must be `AlwaysRunning` and a [daemon](CK.DeviceModel/Daemon) automatically
ensures that they are restarted if it has stopped (typically due to an unexpected error).

By installing the optional [CK.DeviceModel.Configuration](CK.DeviceModel.Configuration/DeviceConfigurator.cs) package,
configurations from the standard .Net configuration API (*appsettings.json* and so on) are automatically and dynamically
applied from the **CK-DeviceModel** root configuration section.

Implementing a new device requires to define its host and its configuration types. Here is a complete implementation of a simple device:

- The host definition (that may offer a specific API if needed, but nothing more that this is required).

```csharp
  public class CameraHost : DeviceHost<Camera, DeviceHostConfiguration<CameraConfiguration>, CameraConfiguration>
  {
  }
```
- The device's configuration: its name MUST end with `Configuration`. The `DoCheckValid` method is optional.

```csharp
  public class CameraConfiguration : DeviceConfiguration
  {
      public CameraConfiguration()
      {
      }

      /// <summary>
      /// The copy constructor is required.
      /// </summary>
      /// <param name="o">The other configuration to be copied.</param>
      public CameraConfiguration( CameraConfiguration o )
          : base( o )
      {
          FlashColor = o.FlashColor;
          FlashRate = o.FlashRate;
      }

      public int FlashColor { get; set; }

      public int FlashRate { get; set; } = 1;
      
      /// <summary>
      /// This uses the monitor to declare/explain errors.
      /// </summary>
      /// <returns>True if the configuration is valid, false otherwise.</returns>
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
  }
```

- The device itself. This is a minimal definition without any Command nor event. It keeps the configuration and handles reconfiguration.

```csharp
  public class Camera : Device<CameraConfiguration>
  {
      // A device can keep a reference to the current configuration:
      // this configuration is an independent clone that is accessible only to the Device.
      CameraConfiguration _configRef;

      public Camera( IActivityMonitor monitor, CreateInfo info )
          : base( monitor, info )
      {
          _configRef = info.Configuration;
      }

      protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config )
      {
          bool configHasChanged = config.FlashColor != _configRef.FlashColor;
          _configRef = config;
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
    /// This command triggers a flash on the camera (and raise our <see cref="Camera.Flash"/> event).
    /// </summary>
    public class FlashCommand : DeviceCommand<CameraHost>
    {
    }

    /// <summary>
    /// The command returns the previous color.
    /// </summary>
    public class SetFlashColorCommand : DeviceCommand<CameraHost,int>
    {
        /// <summary>
        /// The new color to set.
        /// </summary>
        public int Color { get; set; }
    }
```

- Here we define a `Flash` event and we implement the two commands defined above:

```csharp
    ...
    readonly PerfectEventSender<Camera,int> _flash;

    public PerfectEvent<Camera,int> Flash => _flash.PerfectEvent;

    protected override async Task DoHandleCommandAsync( IActivityMonitor monitor,
                                                        BaseDeviceCommand command,
                                                        CancellationToken token )
    {
        switch( command )
        {
            case FlashCommand f:
                await _flash.RaiseAsync( monitor, this, _configRef.FlashColor ).ConfigureAwait( false );
                f.Completion.SetResult();
                return;
            case SetFlashColorCommand c:
                {
                    var prevColor = _configRef.FlashColor;
                    _configRef.FlashColor = c.Color;
                    c.Completion.SetResult( prevColor );
                    return;
                }
        }
        // The base.DoHandleCommandAsync throws a NotSupportedException: all defined commands MUST be handled above!
        await base.DoHandleCommandAsync( monitor, command, token );
    }
```

Notes:
 - [Perfect events](https://github.com/Invenietis/CK-ActivityMonitor/tree/master/CK.PerfectEvent) support Sync, Async and ParrallelAsync handlers and comes with a monitor that callbacks can use.
 - The `Flash` event here is dedicated to the "flash" device event. When multiple device events exist, 
 a generic `AllEvent` that sends a base `MyDeviceEvent` class and multiple specialized events classes is easier
to implement and use (the event handler applies a simple pattern matching on the event argument).

- Below is a helper that triggers a flash directly on the device: such specific API must always be a simple helper 
- that eventually sends a command (an awaits its completion).

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
        await cmd.Completion.Task;
        return true;
    }
```

- To conclude, an example of the *appsettings.json* configuration file for such devices:

```jsonc
{
  // The root section must be CK-DeviceModel.
  "CK-DeviceModel": {
    // Hosts are found thanks to their name. 
    "CameraHost": {
      // Optionally, hosts can have their own configuration if needed, but by default
      // only the Items (the devices) are handled.
      "Items": {
        // Devices are named objects.
        "Camera n°1":
        {
          "Status": "AlwaysRunning",
          "FlashColor": 45,
          "FlashRate": 100
        }
        "Camera n°2":
        {
          "Status": "RunnableStarted",
          "ControllerKey": "WebAPI"
          "FlashColor": 12,
        }
        "Another Camera":
        {
          "Status": "Disabled",
          "FlashColor": 12,
        }
      }
    }
  }
}

```
