#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests;

/// <summary>
/// Component bound to a <see cref="ControllerConfiguration"/>.
/// </summary>
public abstract class AddressableConfiguration : ComponentConfiguration
{
    public AddressableConfiguration()
    {
    }

    public AddressableConfiguration( ICKBinaryReader r )
        : base( r )
    {
        r.ReadByte(); // version
        ControllerName = r.ReadString();
    }

    internal override void Write( ICKBinaryWriter w )
    {
        base.Write( w );
        w.Write( (byte)0 ); // version
        w.Write( ControllerName );
    }

    /// <summary>
    /// Gets or sets this controller name.
    /// </summary>
    public string ControllerName { get; set; } = string.Empty;
}
