using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Common contract that any <see cref="Device{TConfiguration}"/>'s configuration must support.
    /// Actual type used in the generic <see cref="Device{TConfiguration}"/> MUST be an interface: multiple
    /// concrete implementations of a IDeviceConfiguration must be able to coexist and they MUST be substitutable.
    /// </summary>
    public interface IDeviceConfiguration
    {
        /// <summary>
        /// Name of the device. This is a unique key for a device in its host.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="DeviceConfigurationStatus"/>.
        /// </summary>
        DeviceConfigurationStatus ConfigurationStatus { get; set; }

        /// <summary>
        /// Clones the current configuration and returns a new deep copy of the current configuration.
        /// </summary>
        /// <returns>A new configuration of the device.</returns>
        IDeviceConfiguration Clone();

    }
}
