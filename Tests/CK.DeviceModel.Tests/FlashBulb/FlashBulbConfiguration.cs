using CK.Core;

namespace CK.DeviceModel.Tests
{

    public class FlashBulbConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// A default public constructor is required.
        /// </summary>
        public FlashBulbConfiguration()
        {
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

        /// <summary>
        /// Deserialization constructor.
        /// Every specialized configuration MUST define its own deserialization
        /// constructor (that must call its base) and override the <see cref="Write(ICKBinaryWriter)"/>
        /// method (that must start to call its base Write method).
        /// </summary>
        /// <param name="r">The reader.</param>
        public FlashBulbConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            FlashColor = r.ReadInt32();
            FlashRate = r.ReadInt32();
        }

        /// <summary>
        /// Symmetric of the deserialization constructor.
        /// Every Write MUST call base.Write and write a version number.
        /// </summary>
        /// <param name="w">The writer.</param>
        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.Write( FlashColor );
            w.Write( FlashRate );
        }
    }

}

