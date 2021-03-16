# Device & StdDevice

The device base class that should be used is the StdDevice. Its implementation has been
split into two parts: the base device is only responsible of the device life cycle and
the StdDevice extends it to support commands (sync to async adaptation and command/result correlation).

## Device
A device basically exposes its [IDevice](IDevice.cs) interface that publishes its status ([DeviceStatus](DeviceStatus.cs)
and [DeviceConfigurationStatus](../DeviceConfigurationStatus.cs) and a StatusChanged event), exposes its FullName and its
current controller key and supports asynchronous Start, Stop and ExecuteCommand methods.
Configuration (and dynamic reconfiguration) is handled by the [IDeviceHost](../Host/IDeviceHost.cs).

The Device implementation handles the nitty-gritty details of device life cycle but doesn't provide
any implementation for really safe asynchronous command handling, independent monitoring or any events
other than StatusChanged: this is the job of the StdDevice base class.

Specialized devices must provide implementations for:

```csharp
protected abstract Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason );
protected abstract Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, TConfiguration config );
protected abstract Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason );
protected abstract Task DoDestroyAsync( IActivityMonitor monitor );
```

They can add any number of specific methods: they can be seen as multiple instances of  [IHostedService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostedservice)
that support dynamic reconfiguration.

Regarding Commands, the support is minimalist. [DeviceCommand](../Command/DeviceCommand.cs) or [DeviceCommand<TResult>](../Command/DeviceCommandT.cs) can
be submitted to the host (and are routed to the target device thanks to the `DeviceCommand.DeviceName`) or directly to the device (see the 4 `(Unsafe)ExecuteCommandAsync`
methods on [IDevice](IDevice.cs)).

The 2 final handlers available are minimal placeholders that should be overridden:

```csharp
/// <summary>
/// Calls <see cref="ExecuteBasicControlDeviceCommandAsync"/> if the <paramref name="command"/> is a <see cref="BasicControlDeviceCommand"/>
/// otherwise, since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
/// <para>
/// The <paramref name="command"/> object that is targeted to this device (<see cref="DeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/>
/// and <see cref="DeviceCommand.ControllerKey"/> is either null or match the current <see cref="ControllerKey"/>).
/// </para>
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="command">The command to handle.</param>
/// <param name="token">Cancellation token.</param>
/// <returns>The awaitable.</returns>
protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token )
{
    if( command is BasicControlDeviceCommand b )
    {
        return ExecuteBasicControlDeviceCommandAsync( monitor, b );
    }
    else
    {
        throw new ArgumentException( $"Unhandled command {command.GetType().Name}.", nameof( command ) );
    }
}

/// <summary>
/// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
/// <para>
/// The <paramref name="command"/> object that is targeted to this device (<see cref="DeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/>
/// and <see cref="DeviceCommand.ControllerKey"/> is either null or match the current <see cref="ControllerKey"/>).
/// </para>
/// </summary>
/// <param name="monitor">The monitor to use.</param>
/// <param name="command">The command to handle.</param>
/// <param name="token">Cancellation token.</param>
/// <returns>The command's result.</returns>
protected virtual Task<TResult> DoHandleCommandAsync<TResult>( IActivityMonitor monitor, DeviceCommand<TResult> command, CancellationToken token )
{
    throw new ArgumentException( $"Unhandled command {command.GetType().Name}.", nameof( command ) );
}
```

The only command that is define at the Device level is the `BasicControlDeviceCommand` that can trigger the 3 [basic
operations](../Command/BasicControlDeviceOperation.cs).

## StdDevice

The [StdDevice](../StdDevice/StdDevice.cs) extends the device to better support the Command pattern. Key features are:

- Command execution is serialized thanks to an internally managed asynchronous command loop with its own ActivityMonitor and 
a [channel](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels).
- Commands can generate a result (see [DeviceCommand<TResult>](../Command/DeviceCommandT.cs)) or not.
- Commands can be awaited (via the device's `(Unsafe)ExecuteCommandAsync` methods or the host) or not (via the `StdDevice.SendCommand` method).
- Commands that are handled while the device is stopped can be considered as errors, be canceled, still be executed or deferred until the device
 starts again (see the [StoppedBehavior enumeration](../StdDevice/StdDevice.StoppedBehavior.cs).

The 2 base `DoHandleCommandAsync` methods are sealed and replaced by 2 new abstract methods and a few other methods are available:

```csharp
public bool SendCommand( IActivityMonitor monitor, DeviceCommand command, CancellationToken token = default ) { ... }
protected abstract Task HandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token );
protected abstract Task<TResult> HandleCommandAsync<TResult>( IActivityMonitor monitor, DeviceCommand<TResult> command, CancellationToken token );
protected virtual StoppedBehavior OnStoppedDeviceCommand( ActivityMonitor monitor, DeviceCommand command ) => _stoppedBehavior;
protected virtual Task<bool> OnCommandErrorAsync( ActivityMonitor monitor, DeviceCommand command, Exception ex ) => Task.FromResult( true );
```



