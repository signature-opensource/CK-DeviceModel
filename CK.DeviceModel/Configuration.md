# Configuration

## Device Runtime properties & Configuration properties

A property should not be exposed as a device property and also appear on the DeviceConfiguration object.
Sometimes, the choice is not obvious.

## No ConfigurationChanged event

Devices don't expose a `ConfigurationChanged` event (note that devices don't expose their Configuration object).
This is on purpose: devices only expose the `StatusChanged' event that covers the device's status and configuration.

Any specific configuration properties should be handled explicitly with dedicated events and/or properties on the device.

