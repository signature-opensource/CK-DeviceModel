namespace CK.DeviceModel.Tests
{
    public class MachineHost : DeviceHost<Machine, DeviceHostConfiguration<MachineConfiguration>, MachineConfiguration>
    {
        public MachineHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }
    }

}

