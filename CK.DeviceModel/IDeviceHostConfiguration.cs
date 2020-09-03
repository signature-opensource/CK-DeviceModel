using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic abstration of the host configuration.
    /// </summary>
    public interface IDeviceHostConfiguration
    {
        /// <summary>
        /// Gets whether this is a partial configuration.
        /// </summary>
        bool IsPartialConfiguration { get; }

        /// <summary>
        /// Gets the configurations.
        /// </summary>
        IReadOnlyList<IDeviceConfiguration> Configurations { get; }
    }

    /// <summary>
    /// Extends host configuration.
    /// </summary>
    public static class DeviceHostConfigurationExtension
    {
        /// <summary>
        /// Clones the current configuration and returns a new deep copy of this current configuration.
        /// </summary>
        /// <remarks>
        /// This generic clone function relies on the copy constructor of <see cref="DeviceHostConfiguration{TConfiguration}"/>.
        /// </remarks>
        /// <returns>A new configuration.</returns>
        public static T Clone<T>( this T config ) where T : IDeviceHostConfiguration
        {
            return (T)Activator.CreateInstance( typeof( T ), new object[] { config } );
        }
    }

}
