using CK.DeviceModel.CppDeviceAdapter.Cpp;
using System;
using System.Runtime.InteropServices;

namespace CK.DeviceModel.CppDeviceAdapter.Tests
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TestCppDeviceConfig : ICppNativeDeviceConfig
    {
        public int TimeMs;

        public int MaxCount;

        public TestCppDeviceConfig(TimeSpan timer, int maxCount)
        {
            TimeMs = (int)Math.Floor(timer.TotalMilliseconds);
            MaxCount = maxCount;
        }

        public ICppNativeDeviceConfig Clone()
        {
            return new TestCppDeviceConfig(TimeSpan.FromMilliseconds(TimeMs), MaxCount);
        }
    }
}
