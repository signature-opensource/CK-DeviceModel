#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests
{
    public class BooleanSensorConfiguration : AddressableConfiguration
    {
        public BooleanSensorConfiguration()
        {
        }

        public BooleanSensorConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            FieldName = r.ReadString();
        }

        internal override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 ); // version
            w.Write( FieldName );
        }

        public string FieldName { get; set; } = string.Empty;
    }
}
