# ActiveDevice

An [ActiveDevice](ActiveDevice.cs) is a specialized [Device](../Device/Device.cs) that implements
an event dispatch loop in addition to the command loop of any device.

This base class has 2 goals:
- Simplifying implementations (like any instance of the *template method* pattern) of device that in
addition to handle commands must also emit events to the external world.
- Offer a standardized API on all devices.

## Unified event API

The [IActiveDevice](IActiveDevice.cs) is a non-generic interface that is shared by any active device.
