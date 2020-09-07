using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic abstraction of the host configuration.
    /// </summary>
    public interface IDeviceHostConfiguration : ICloneableCopyCtor
    {
        /// <summary>
        /// Gets whether this is a partial configuration.
        /// </summary>
        bool IsPartialConfiguration { get; }

        /// <summary>
        /// Gets the configurations.
        /// </summary>
        IReadOnlyList<DeviceConfiguration> Configurations { get; }
    }
}
