using CK.Core;

namespace CK.DeviceModel.Tests
{

    public class FlashBulbConfiguration : DeviceConfiguration
    {
        public FlashBulbConfiguration()
        {
        }

        /// <summary>
        /// The copy constructor is required.
        /// </summary>
        /// <param name="o">The other configuration to be copied.</param>
        public FlashBulbConfiguration( FlashBulbConfiguration o )
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

        public FlashBulbConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            FlashColor = r.ReadInt32();
            FlashRate = r.ReadInt32();
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.Write( FlashColor );
            w.Write( FlashRate );
        }

    }

}

