namespace CK.DeviceModel.Tests
{

    public class CameraConfiguration : DeviceConfiguration
    {
        public CameraConfiguration()
        {
        }

        public CameraConfiguration( CameraConfiguration o )
            : base( o )
        {
            FlashColor = o.FlashColor;
        }

        public int FlashColor { get; set; }
    }

}

