using CK.DeviceModel.CppDeviceAdapter.Cpp;
using System;

namespace CK.DeviceModel.CppDeviceAdapter.Tests
{
    public class TimeoutTestDeviceConfiguration : ICppDeviceConfiguration
    {
        public string Name { get ; set ; }
        public DeviceConfigurationStatus ConfigurationStatus { get; set; }
        public ICppNativeDeviceConfig NativeDeviceConfig { get; set; }

        int _maxCount;

        TimeSpan _cycleDuration;

        public IDeviceConfiguration Clone()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration(Name, _cycleDuration, _maxCount, NativeDeviceConfig);
            return config;
        }

        public TimeoutTestDeviceConfiguration(string name, TimeSpan cycleDuration, int maxCount, ICppNativeDeviceConfig nativeDevConfig)
        {
            Name = name;
            _cycleDuration = TimeSpan.FromMilliseconds(cycleDuration.TotalMilliseconds);
            _maxCount = maxCount;
            NativeDeviceConfig = nativeDevConfig;
        }
    }
}
