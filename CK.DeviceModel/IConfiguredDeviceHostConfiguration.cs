using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    public interface IConfiguredDeviceHostConfiguration<TConfiguration> where TConfiguration : IDeviceConfiguration
    {
        /// <summary>
        /// Gets or sets whether this is a partial configuration: <see cref="Configurations"/> will be applied 
        /// but existing devices without configurations are let as-is.
        /// </summary>
        bool IsPartialConfiguration { get; set; }

        /// <summary>
        /// Gets a mutable list of configurattions.
        /// <see cref="IDeviceConfiguration.Name"/> must be unique: this will be checked when this 
        /// configuration will be applied.
        /// </summary>
        IList<TConfiguration> Configurations { get; }

        /// <summary>
        /// Clones the current configuration and returns a new deep copy of this current configuration.
        /// </summary>
        /// <returns>A new, cloned, configuration.</returns>
        IConfiguredDeviceHostConfiguration<TConfiguration> Clone();
    }
}
