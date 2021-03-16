using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Configuration for <see cref="StdDevice{TConfiguration}"/>.
    /// </summary>
    public class StdDeviceConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Initializes a new device configuration with an empty name and a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected StdDeviceConfiguration()
        {
        }

        /// <summary>
        /// Copy constructor (see <see cref="ICloneableCopyCtor"/>).
        /// Specialized configurations MUST implement their copy constructor.
        /// </summary>
        /// <param name="source">The source configuration to copy.</param>
        protected StdDeviceConfiguration( DeviceConfiguration source )
            : base( source )
        {
        }
    }
}
