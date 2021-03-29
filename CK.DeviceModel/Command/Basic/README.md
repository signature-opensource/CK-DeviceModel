# Basic commands

The 5 basic commands follow the same pattern:

- The BaseXXX cannot be specialized outside of the CK.DeviceModel internals.

- The StoppedBehavior is "RunAnyway" (they can run on a stopped device).

- They can be sent via SendCommand or SendCommandImmediate or are simply useless since the 5 device's methods: 
SetControllerKeyAsync, StartAsync, StopAsync, ReconfigureAsync or DestroyAsync (see [IDevice](../../Device/IDevice.cs))
can execute them immediately.

- They all respect the `ControllerKey` optional guard.

Specificities are:
- [Destroy](BaseDestroyDeviceCommand.cs) has no result and always succeeds.
- [Start](StartDeviceCommand.cs) and [Stop](StopDeviceCommand.cs) are boolean commands. Exceptions or cancellation are transformed into a `false' result.
- Stop can specify `IgnoreAlwaysRunning` to stop a device even if its configuration state that it must be `AlwaysRunning`.
- [ReconfigureDeviceCommand&lt;TConfiguration&gt;](ReconfigureDeviceCommand.cs) is parametrized.
- [SetControllerKey](SetControllerKeyDeviceCommand.cs) exposes the `NewControllerKey` (in addition to the standard `ControllerKey` guard).

![The 5 basic commands](/../../../Common/Doc/BasicCommands.png)

