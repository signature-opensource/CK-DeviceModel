namespace CK.DeviceModel.Tests
{
    public class CameraHost : DeviceHost<Camera, DeviceHostConfiguration<CameraConfiguration>, CameraConfiguration>
    {
        public CameraHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }
    }

}

