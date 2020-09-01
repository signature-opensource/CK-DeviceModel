using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.CppDeviceAdapter.Cpp
{
    public interface ICppDeviceConfiguration : IDeviceConfiguration
    {

        ICppNativeDeviceConfig NativeDeviceConfig { get; set; }

    }
}
