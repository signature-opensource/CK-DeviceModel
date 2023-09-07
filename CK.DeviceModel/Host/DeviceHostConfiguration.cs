using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Default implementation of a host device configuration.
    /// This class may be specialized if needed (recall to implement its binary serialization/deserialization).
    /// <para>
    /// Just like DeviceCofiguration, concrete DeviceHostConfiguration classes should be sealed since simple
    /// binary de/serialization and auto configuration don't support polymorphism.
    /// </para>
    /// </summary>
    /// <typeparam name="TConfiguration"></typeparam>
    public class DeviceHostConfiguration<TConfiguration> : IDeviceHostConfiguration where TConfiguration : DeviceConfiguration, new()
    {
        /// <summary>
        /// Initializes a new empty partial host configuration:
        /// since it is partial, it cannot hurt.
        /// </summary>
        public DeviceHostConfiguration()
        {
            IsPartialConfiguration = true;
            Items = new List<TConfiguration>();
        }

        /// <summary>
        /// Deserialization constructor.
        /// In a specialized host, it must be implemented just like <see cref="DeviceConfiguration(ICKBinaryReader)"/>.
        /// </summary>
        /// <param name="r">The reader.</param>
        public DeviceHostConfiguration( ICKBinaryReader r )
        {
            r.ReadByte(); // version.
            IsPartialConfiguration = r.ReadBoolean();
            int c = r.ReadNonNegativeSmallInt32();
            Items = new List<TConfiguration>( c );
            var callParam = new object[] { r };
            while( --c >= 0 )
            {
                Items.Add( (TConfiguration)Activator.CreateInstance( typeof( TConfiguration ), callParam )! );
            }
        }

        /// <summary>
        /// Writes this host configuration.
        /// In a specialized host, it must be implemented just like <see cref="DeviceConfiguration.Write(ICKBinaryWriter)"/>.
        /// </summary>
        /// <param name="w"></param>
        public virtual void Write( ICKBinaryWriter w )
        {
            w.Write( (byte)0 );
            w.Write( IsPartialConfiguration );
            w.WriteNonNegativeSmallInt32( Items.Count );
            foreach( var c in Items ) c.Write( w );
        }

        /// <summary>
        /// Gets or sets whether this is a partial configuration: <see cref="Items"/> will be applied 
        /// but existing devices without configurations are left as-is.
        /// Defaults to true.
        /// <para>
        /// When set to false, this configuration destroys all devices for which no configuration exists in the <see cref="Items"/>.
        /// </para>
        /// </summary>
        public bool IsPartialConfiguration { get; set; }

        /// <summary>
        /// Gets a mutable list of configurations.
        /// <see cref="DeviceConfiguration.Name"/> must be unique: this will be checked when this 
        /// configuration will be applied.
        /// </summary>
        public List<TConfiguration> Items { get; }

        IReadOnlyList<DeviceConfiguration> IDeviceHostConfiguration.Items => (IReadOnlyList<DeviceConfiguration>)Items;

        void IDeviceHostConfiguration.Add( DeviceConfiguration c ) => Items.Add( (TConfiguration)c );

        /// <inheritdoc />
        public bool CheckValidity( IActivityMonitor monitor, bool allowEmptyConfiguration )
        {
            var dedup = new HashSet<string>();
            bool success = true;
            int idx = 0;
            foreach( var c in Items )
            {
                ++idx;
                if( !c.CheckValid( monitor ) )
                {
                    monitor.Error( $"Configuration n°{idx} (name = '{c.Name}') is not valid." );
                    success = false;
                }
                if( !dedup.Add( c.Name ) )
                {
                    monitor.Error( $"Duplicate configuration found: '{c.Name}'. Configuration names must be unique." );
                    success = false;
                }
            }
            if( idx == 0 && !allowEmptyConfiguration )
            {
                monitor.Error( $"Empty configuration is not allowed." );
                success = false;
            }
            return success && DoCheckValidity( monitor );
        }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor, bool)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }

}
