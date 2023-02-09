#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;

namespace CK.DeviceModel.Configuration.Tests
{
    /// <summary>
    /// A span is a physical passive piece of <see cref="HubConfiguration"/> with a length.
    /// </summary>
    public class SpanConfiguration : ComponentConfiguration
    {
        public SpanConfiguration()
        {
        }

        public SpanConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            Length = r.ReadUInt32();
        }

        internal override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 ); // version
            w.Write( Length );
        }

        /// <summary>
        /// Gets or set the length of this span.
        /// </summary>
        public uint Length { get; set; }
    }
}
