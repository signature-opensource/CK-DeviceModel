# DeviceHost

Implementing a new device requires to implement:
- The device itself that specializes the abstract [Device&lt;TConfiguration&gt;](../Device/Device.cs).
- The corresponding configuration that specializes the abstract [DeviceConfiguration](../DeviceConfiguration.cs).
- The host for this type of devices that specializes the abstract [DeviceHost&lt;T, THostConfiguration, TConfiguration&gt;](DeviceHost.cs).

## DeviceHostConfiguration

A host MAY have a specialized [DeviceHostConfiguration&lt;TConfiguration&gt;](DeviceHostConfiguration.cs) where *TConfiguration* is the actual
device's configuration, but this is totally optional. The DeviceHostConfiguration is not abstract and can be used directly so that
a typical host implementation is an empty class that uses the `DeviceHostConfiguration<TConfiguration>` as its own configuration:

```csharp
public class FlashBulbHost : DeviceHost<FlashBulb, DeviceHostConfiguration<FlashBulbConfiguration>, FlashBulbConfiguration>
{
}
```

The host configuration exposes the `List<TConfiguration> Items` with all the device's configuration that must be applied. By default,
a host configuration is **"partial"**: it will ignore all existing devices that don't appear in its `Items`.
This behavior can be changed thanks to the `IsPartialConfiguration` property:

```csharp
    /// <summary>
    /// Gets or sets whether this is a partial configuration: <see cref="Items"/> will be applied 
    /// but existing devices without configurations are let as-is.
    /// Defaults to true.
    /// <para>
    /// When set to false, this configuration destroys all devices for which no configuration exists in the <see cref="Items"/>.
    /// </para>
    /// </summary>
    public bool IsPartialConfiguration { get; set; }
```

> Host can expose dedicated properties, methods or events with its own specialized configuration but, as of today, 
> we've not used this capability for any device.

Like the other component of the CK-DeviceModel, host expose both a non-generic API via its [IDeviceHost](IDeviceHost.cs) interface
and is strongly typed at its own level (hence the numerous generic parameters).
The *IDeviceHost* interface is easy to understand.

## Lifetime of a DeviceHost

A host is a multiple singleton service: each instance of **concrete DeviceHost* type exists
only once but their *IDeviceHost* interface (that is marked with *[IsMultiple]* attribute) is
registered as a `IEnumerable<IDeviceHost>` of hosts.

For those are curious, this simply results of the way the interface and the base class are defined.

- IDeviceHost is defined as:
```csharp
    [IsMultiple]
    public interface IDeviceHost : ISingletonAutoService { ... }
```
- And the DeviceHost base class is:
```csharp
    [CKTypeDefiner]
    public abstract partial class DeviceHost<T, THostConfiguration, TConfiguration> : IDeviceHost
        where T : Device<TConfiguration>
        where THostConfiguration : DeviceHostConfiguration<TConfiguration>
        where TConfiguration : DeviceConfiguration
    { ... }
```

As a singleton service, a host is created whenever the application DI container requires
one instance of its type and remains alive until the end of the container (more often the end
the process).

In practice, another participant interacts with the hosts lifetime: the [DeviceHostDaemon](../Daemon),
as a *IHostedService* is automatically instantiated at the start of the application and it depends
on the `IEnumerable<IDeviceHost>`. This de facto instantiates all the hosts when the application
starts.

A host is not a *IDisposable* object however its `IDeviceHost.ClearAsync` method destroys all its devices.
When stopped, the daemon can call this on all the hosts, explicitly destroying all the devices instead of
let them die with the process: see [DeviceHostDaemon](../Daemon).

