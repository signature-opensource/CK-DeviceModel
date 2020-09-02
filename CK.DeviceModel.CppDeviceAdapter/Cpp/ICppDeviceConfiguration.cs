using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.CppDeviceAdapter.Cpp
{
    public interface ICppDeviceConfiguration : IDeviceConfiguration
    {
        /// <summary>
        /// Contains the configuration of the device on the C++ side.
        /// </summary>
        ICppNativeDeviceConfig NativeDeviceConfig { get; set; }

    }
}
