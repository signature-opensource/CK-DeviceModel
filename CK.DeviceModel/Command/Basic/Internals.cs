using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    internal class InternalReconfigureDeviceCommand<TConfiguration> : ReconfigureDeviceCommand<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        public InternalReconfigureDeviceCommand( Type hostType, string name )
        {
            HostType = hostType;
            DeviceName = name;
        }
        public override Type HostType { get; }
        public TConfiguration? ExternalConfig { get; internal set; }
    }

    internal class InternalStartDeviceCommand : StartDeviceCommand
    {
        public InternalStartDeviceCommand( Type hostType, string name )
        {
            HostType = hostType;
            DeviceName = name;
        }

        public override Type HostType { get; }
    }

    internal class InternalStopDeviceCommand : StopDeviceCommand
    {
        public InternalStopDeviceCommand( Type hostType, string name, bool ignoreAlwaysRunning )
        {
            HostType = hostType;
            DeviceName = name;
            IgnoreAlwaysRunning = ignoreAlwaysRunning;
        }

        public override Type HostType { get; }
    }

    internal class InternalDestroyDeviceCommand : DestroyDeviceCommand
    {
        public InternalDestroyDeviceCommand( Type hostType, string name )
        {
            HostType = hostType;
            DeviceName = name;
        }

        public override Type HostType { get; }
    }

    internal class InternalSetControllerKeyDeviceCommand : SetControllerKeyDeviceCommand
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

}
