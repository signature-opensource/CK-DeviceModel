using CK.Core;

namespace CK.DeviceModel.Tests
{

    public class OtherMachineConfiguration : DeviceConfiguration
    {
        public OtherMachineConfiguration()
        {
        }

        public OtherMachineConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
        }
    }

}

