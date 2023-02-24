#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests
{
    public class MotorConfiguration : AddressableConfiguration
    {
        public MotorConfiguration()
        {
        }

        public MotorConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            ForwardFieldName = r.ReadNullableString();
            Length = r.ReadSmallInt32();
        }

        internal override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 ); // version
            w.WriteNullableString( ForwardFieldName );
            w.WriteSmallInt32( Length );
        }

        public string? ForwardFieldName { get; set; }

        public int Length { get; set; }
    }
}
