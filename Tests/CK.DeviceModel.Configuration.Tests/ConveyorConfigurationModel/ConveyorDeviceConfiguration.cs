#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace CK.DeviceModel.Configuration.Tests
{
    public class ConveyorDeviceConfiguration : DeviceConfiguration
    {
        public ConveyorDeviceConfiguration()
        {
        }

        public ConveyorDeviceConfiguration( IActivityMonitor monitor, IConfigurationSection configuration )
        {
            configuration.CreateSectionWithout(nameof(Hubs)).Bind( this );
            foreach( var hC in configuration.GetSection( nameof( Hubs ) ).GetChildren() )
            {
                if( !hC.Exists() )
                {
                    monitor.Warn( $"Hub named '{hC.Key}' (path: '{hC.Path}') is empty. it is ignored." );
                }
                else
                {
                    Hubs.Add( hC.Key, new HubConfiguration( monitor, hC ) );
                }
            }
        }

        public string? OneProp { get; set; }

        public int AnotherProp { get; set; }

        public Dictionary<string,HubConfiguration> Hubs { get; } = new();

        public ConveyorDeviceConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            OneProp = r.ReadNullableString();
            AnotherProp = r.ReadInt32();
            int c = r.ReadSmallInt32();
            while( --c >= 0 )
            {
                Hubs.Add( r.ReadString(), new HubConfiguration( r ) );
            }
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.WriteNullableString( OneProp );
            w.Write( AnotherProp );
            w.WriteSmallInt32( Hubs.Count );
            foreach( var (name,h) in Hubs )
            {
                w.Write( name );
                h.Write( w );
            }
        }
    }
}
