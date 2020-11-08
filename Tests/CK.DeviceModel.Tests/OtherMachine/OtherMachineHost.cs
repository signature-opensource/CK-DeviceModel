namespace CK.DeviceModel.Tests
{
    public class OtherMachineHost : DeviceHost<OtherMachine, DeviceHostConfiguration<OtherMachineConfiguration>, OtherMachineConfiguration>
    {
        public OtherMachineHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }
    }

}

