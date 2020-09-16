using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic abstraction of the host configuration.
    /// </summary>
    public interface IDeviceHostConfiguration : ICloneableCopyCtor
    {
        /// <summary>
        /// Gets whether this is a partial configuration.
        /// Defaults to true.
        /// </summary>
        bool IsPartialConfiguration { get; }

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
    }
}
