using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic abstraction of the host configuration.
    /// </summary>
    public interface IDeviceHostConfiguration : ICKSimpleBinarySerializable
    {
        /// <summary>
        /// Gets or sets whether this is a partial configuration.
        /// Defaults to true.
        /// </summary>
        bool IsPartialConfiguration { get; set; }

        /// <summary>
        /// Gets the devices' configuration.
        /// </summary>
        IReadOnlyList<DeviceConfiguration> Items { get; }

        /// <summary>
        /// Adds the device configuration to the <see cref="Items"/> list.
        /// Note that the configuration's type must be the actual one otherwise a <see cref="InvalidCastException"/> will be thrown.
        /// </summary>
        /// <param name="c">The device configuration.</param>
        void Add( DeviceConfiguration c );

        /// <summary>
        /// Checks the validity of this configuration: all <see cref="DeviceConfiguration.Name"/> must be non empty or white space, be
        /// unique among the different configurations, and optionally, at least one configuration must exist.
        /// This calls <see cref="DeviceConfiguration.CheckValid(IActivityMonitor)"/> for each configuration.
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <param name="allowEmptyConfiguration">False to consider an empty configuration as an error.</param>
        /// <returns>Whether this configuration is valid.</returns>
        bool CheckValidity( IActivityMonitor monitor, bool allowEmptyConfiguration );
    }
}
