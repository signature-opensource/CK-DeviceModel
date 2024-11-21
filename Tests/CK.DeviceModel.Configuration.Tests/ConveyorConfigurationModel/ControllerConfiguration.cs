#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests;

public class ControllerConfiguration : ComponentConfiguration
{
    public ControllerConfiguration()
    {
    }

    public ControllerConfiguration( ICKBinaryReader r )
        : base( r )
    {
        r.ReadByte(); // version
        ManufacturerDeviceName = r.ReadString();
        Address = r.ReadUInt32();
    }

    internal override void Write( ICKBinaryWriter w )
    {
        base.Write( w );
        w.Write( (byte)0 ); // version
        w.Write( ManufacturerDeviceName );
        w.Write( Address );
    }

    public string ManufacturerDeviceName { get; set; } = string.Empty;

    public uint Address { get; set; }
}
