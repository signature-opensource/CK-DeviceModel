using CK.Core;

namespace CK.DeviceModel.Tests
{

    public class CameraConfiguration : DeviceConfiguration
    {
        public CameraConfiguration()
        {
        }

        /// <summary>
        /// The copy constructor is required.
        /// </summary>
        /// <param name="o">The other configuration to be copied.</param>
        public CameraConfiguration( CameraConfiguration o )
            : base( o )
        {
            FlashColor = o.FlashColor;
            FlashRate = o.FlashRate;
        }

        public int FlashColor { get; set; }

        public int FlashRate { get; set; } = 1;

        protected override bool DoCheckValid( IActivityMonitor monitor )
        {
            bool isValid = true;
            if( FlashColor < 0 || FlashColor > 3712 )
            {
                monitor.Error( $"FlashColor must be between 0 and 3712." );
                isValid = false;
            }
            if( FlashRate <= 0 )
            {
                monitor.Error( $"FlashRate must be positive." );
                isValid = false;
            }
            return isValid;
        }
    }

}

