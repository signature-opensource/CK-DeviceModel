using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace CK.DeviceModel
{

    /// <summary>
    /// Default implementation of a DeviceHost configuration.
    /// This class can be specialized if needed.
    /// </summary>
    /// <typeparam name="TConfiguration"></typeparam>
    public class DeviceHostConfiguration<TConfiguration> : IDeviceHostConfiguration where TConfiguration : IDeviceConfiguration
    {
        /// <summary>
        /// Initializes a new empty partial host configuration:
        /// since it is partial, it cannot hurt.
        /// </summary>
        public DeviceHostConfiguration()
        {
            IsPartialConfiguration = true;
            Configurations = new List<TConfiguration>();
        }

        /// <summary>
        /// Copy constructor implements the required <see cref="Clone"/> method:
        /// specialized configurations must implement their copy constructor.
        /// </summary>
        /// <param name="source">The source configuration to copy.</param>
        public DeviceHostConfiguration( DeviceHostConfiguration<TConfiguration> source )
        {
            IsPartialConfiguration = source.IsPartialConfiguration;
            Configurations = source.Configurations.Select( c => (TConfiguration)c.Clone() ).ToList();
        }

        /// <summary>
        /// Gets or sets whether this is a partial configuration: <see cref="Configurations"/> will be applied 
        /// but existing devices without configurations are let as-is.
        /// Defaults to true.
        /// <para>
        /// When set to false, this configuration destroys all devices for which no configuration exists in the <see cref="Configurations"/>.
        /// </para>
        /// </summary>
        public bool IsPartialConfiguration { get; set; }

        /// <summary>
        /// Gets a mutable list of configurations.
        /// <see cref="IDeviceConfiguration.Name"/> must be unique: this will be checked when this 
        /// configuration will be applied.
        /// </summary>
        public IList<TConfiguration> Configurations { get; }

        IReadOnlyList<IDeviceConfiguration> IDeviceHostConfiguration.Configurations => (IReadOnlyList<IDeviceConfiguration>)Configurations;

        /// <summary>
        /// Checks the validity of this configuration: all <see cref="IDeviceConfiguration.Name"/> must be non empy or white space, be
        /// unique among the different configurations, and optionally, at least one configuration must exist.
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <param name="allowEmptyConfiguration">False to consider an empty configuration as an error.</param>
        /// <returns>Whether this configuration is valid.</returns>
        public bool CheckValidity( IActivityMonitor monitor, bool allowEmptyConfiguration )
        {
            var dedup = new HashSet<string>();
            bool success = true;
            int idx = 0;
            foreach( var c in Configurations )
            {
                ++idx;
                var name = c.Name;
                if( String.IsNullOrWhiteSpace( name ) )
                {
                    monitor.Error( $"Configuration n°{idx}: Configuration name is invalid." );
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
            return success ? DoCheckValidity( monitor ) : false;
        }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor, bool)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }

}
