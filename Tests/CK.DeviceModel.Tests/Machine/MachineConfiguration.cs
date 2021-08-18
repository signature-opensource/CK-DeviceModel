using CK.Core;

namespace CK.DeviceModel.Tests
{

    public class MachineConfiguration : DeviceConfiguration
    {
        public MachineConfiguration()
        {
        }

        public MachineConfiguration( MachineConfiguration o )
            : base( o )
        {
        }

        public MachineConfiguration( ICKBinaryReader r )
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

