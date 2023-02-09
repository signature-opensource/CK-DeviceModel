#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.DeviceModel.Configuration.Tests
{
    public class HubConfiguration
    {
        internal HubConfiguration( ICKBinaryReader r )
        {
            r.ReadByte(); // version
            IPAddress = r.ReadNullableString();
            TimeoutMilliseconds = r.ReadInt32();
            int c = r.ReadSmallInt32();
            while( --c >= 0 )
            {
                var comp = ReadComponent( r );
                Components.Add( comp.Name, comp );
            }

            static ComponentConfiguration ReadComponent( ICKBinaryReader r )
            {
                return r.ReadByte() switch
                {
                    0 => new SpanConfiguration( r ),
                    1 => new BooleanSensorConfiguration( r ),
                    2 => new MotorConfiguration( r ),
                    3 => new ControllerConfiguration( r ),
                    _ => Throw.InvalidDataException<ComponentConfiguration>()
                };
            }
        }

        internal void Write( ICKBinaryWriter w )
        {
            w.Write( (byte)0 ); // version
            w.WriteNullableString( IPAddress );
            w.Write( TimeoutMilliseconds );
            w.WriteSmallInt32( Components.Count );
            foreach( var c in Components.Values )
            {
                w.Write( (byte)( c switch
                {
                    ControllerConfiguration => 3,
                    MotorConfiguration => 2,
                    BooleanSensorConfiguration => 1,
                    SpanConfiguration => 0,
                    _ => Throw.InvalidDataException<byte>()
                }));
                c.Write( w );
            }
        }

        internal HubConfiguration( IActivityMonitor monitor, IConfigurationSection configuration )
        {
            configuration.CreateSectionWithout( nameof( Components ) ).Bind( this );
            foreach( var cC in configuration.GetSection( nameof( Components ) ).GetChildren() )
            {
                if( !cC.Exists() )
                {
                    monitor.Warn( $"Component named '{cC.Key}' is an empty section. it is ignored." );
                }
                else
                {
                    var c = CreateComponent( monitor, cC );
                    if( c != null )
                    {
                        Debug.Assert( c.Name == cC.Key );
                        Components.Add( c.Name, c );
                    }
                }
            }

            static ComponentConfiguration? CreateComponent( IActivityMonitor monitor, IConfigurationSection c )
            {
                var tName = c["Type"];
                if( string.IsNullOrWhiteSpace( tName ) )
                {
                    monitor.Error( $"Missing or invalid Type name for '{c.Path}'." );
                    return null;
                }
                // Unfortunately Configuration keys are case insensitive (one cannot use a nice switch expression
                // without hideous ToLowerInvariant). If one need more than this few types, these if can be replaced
                // by a static Dictionary<string,Type> mapping.
                Type t;
                if( tName.Equals( "Span", StringComparison.OrdinalIgnoreCase ) )
                {
                    t = typeof( SpanConfiguration );
                }
                else if( tName.Equals( "Controller", StringComparison.OrdinalIgnoreCase ) )
                {
                    t = typeof( ControllerConfiguration );
                }
                else if( tName.Equals( "Motor", StringComparison.OrdinalIgnoreCase ) )
                {
                    t = typeof( MotorConfiguration );
                }
                else if( tName.Equals( "BooleanSensor", StringComparison.OrdinalIgnoreCase ) )
                {
                    t = typeof( BooleanSensorConfiguration );
                }
                else
                {
                    monitor.Error( $"Unknown Type name '{tName}' for '{c.Path}'." );
                    return null;
                }
                var result = (ComponentConfiguration?)c.Get( t );
                Debug.Assert( result != null );
                result.Name = c.Key;
                return result;
            }
        }

        public string? IPAddress { get; set; }

        public int TimeoutMilliseconds { get; set; }

        public Dictionary<string,ComponentConfiguration> Components { get; } = new();
    }
}
