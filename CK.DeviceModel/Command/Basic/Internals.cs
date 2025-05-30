using System;

namespace CK.DeviceModel;

internal class InternalConfigureDeviceCommand<TConfiguration> : BaseConfigureDeviceCommand<TConfiguration>
    where TConfiguration : DeviceConfiguration, new()
{
    public InternalConfigureDeviceCommand( Type hostType, DeviceConfiguration? externalConfiguration, DeviceConfiguration? clonedConfiguration, (string lockedName, string? controllerKey)? locked = null )
        : base( (TConfiguration?)externalConfiguration, (TConfiguration?)clonedConfiguration, locked )
    {
        HostType = hostType;
    }

    public override Type HostType { get; }
}

internal class InternalStartDeviceCommand : BaseStartDeviceCommand
{
    public InternalStartDeviceCommand( Type hostType, string name )
    {
        HostType = hostType;
        DeviceName = name;
    }

    public override Type HostType { get; }
}

internal class InternalStopDeviceCommand : BaseStopDeviceCommand
{
    public InternalStopDeviceCommand( Type hostType, string name, bool ignoreAlwaysRunning )
    {
        HostType = hostType;
        DeviceName = name;
        IgnoreAlwaysRunning = ignoreAlwaysRunning;
    }

    public override Type HostType { get; }
}

internal class InternalDestroyDeviceCommand : BaseDestroyDeviceCommand
{
    public InternalDestroyDeviceCommand( Type hostType, string name )
    {
        HostType = hostType;
        DeviceName = name;
    }

    public override Type HostType { get; }
}

internal class InternalSetControllerKeyDeviceCommand : BaseSetControllerKeyDeviceCommand
{
    public InternalSetControllerKeyDeviceCommand( Type hostType, string name, string? current, string? newControllerKey )
    {
        HostType = hostType;
        DeviceName = name;
        ControllerKey = current;
        NewControllerKey = newControllerKey;
    }

    public override Type HostType { get; }
}
