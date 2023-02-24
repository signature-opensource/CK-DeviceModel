#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests
{
    /// <summary>
    /// Components have a unique <see cref="Name"/> and a <see cref="Position"/>
    /// in a <see cref="HubConfiguration"/>.
    /// </summary>
    public abstract class ComponentConfiguration
    {
        public ComponentConfiguration()
        {
        }

        internal ComponentConfiguration( ICKBinaryReader r )
        {
            r.ReadByte(); // version
            Name = r.ReadString();
            Position = r.ReadUInt32();
        }

        internal virtual void Write( ICKBinaryWriter w )
        {
            w.Write( (byte)0 );
            w.Write( Name );
            w.Write( Position );
        }

        /// <summary>
        /// Gets or sets the name of this component that must be unique in the <see cref="HubConfiguration"/>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the position in centimeter from the start of the <see cref="HubConfiguration"/>.
        /// </summary>
        public uint Position { get; set; }
    }
}
