using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    public interface IDeviceConfiguration
    {
        /// <summary>
        /// Name of the device.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Clones the current configuration and returns a new deep copy of the current configuration
        /// </summary>
        /// <returns>A handle to the new configuration.</returns>
        public IDeviceConfiguration Clone();

        //public T CreateDevice<T>() where T : Device;
    }
}
