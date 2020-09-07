using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for configuration.
    /// </summary>
    public abstract class DeviceConfiguration : ICloneableCopyCtor
    {
        /// <summary>
        /// Initializes a new device configuration with an empty name and a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected DeviceConfiguration()
        {
            Name = String.Empty;
        }

        /// <summary>
        /// Copy constructor (see <see cref="ICloneableCopyCtor"/>).
        /// Specialized configurations MUST implement their copy constructor.
        /// </summary>
        /// <param name="source">The source configuration to copy.</param>
        protected DeviceConfiguration( DeviceConfiguration source )
        {
            Name = source.Name;
            ConfigurationStatus = source.ConfigurationStatus;
        }

        /// <summary>
        /// Gets or sets the name of the device.
        /// This is a unique key for a device in its host.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="DeviceConfigurationStatus"/>.
        /// </summary>
        public DeviceConfigurationStatus ConfigurationStatus { get; set; }

    }
}
