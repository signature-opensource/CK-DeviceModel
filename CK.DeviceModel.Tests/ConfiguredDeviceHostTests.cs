using NUnit.Framework;
using FluentAssertions;
using CK.DeviceModel.LanguageSpecificDevices.Cpp;
using System;
using System.Runtime.CompilerServices;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class ConfiguredDeviceHostTests
    {

        public class TestCameraConfig : ICppDeviceConfiguration
        {
            public string Name { get; set; }

            public IDeviceConfiguration Clone()
            {
                throw new NotImplementedException();
            }
        }

        public class TestCameraNativeConfig : ICppNativeDeviceConfig
        {

        }

        public class ConfiguredDeviceHostTestConfiguration : IConfiguredDeviceHostConfiguration
        {

        }

        public class TestCamera : CppDevice
        {
            public TestCamera(ICppDeviceConfiguration config, ICppNativeDeviceConfig cppNativeDeviceConfig) : base(config, cppNativeDeviceConfig)
            {

            }

            public override long? ExternalGUID()
            {
                throw new NotImplementedException();
            }

            public override string ExternalIdentifier()
            {
                throw new NotImplementedException();
            }

            public override bool Start(bool useThread = true)
            {
                throw new NotImplementedException();
            }

            public override bool Stop()
            {
                throw new NotImplementedException();
            }

            protected override IntPtr CreateCppNativeDevice(IntPtr configPtr)
            {
                throw new NotImplementedException();
            }

            protected override long GetDeviceID()
            {
                throw new NotImplementedException();
            }

            protected override bool RegisterEventsProcessingCallbackToCppNativeDevice(IntPtr ptrToEncapsulatedCppNativeDevice, IntPtr callbackPtr)
            {
                throw new NotImplementedException();
            }
        }

        public class PCLConfiguration : IDeviceConfiguration
        {
            public string Name { get; set; }

            public IDeviceConfiguration Clone()
            {
                throw new NotImplementedException();
            }

        }

        public class PCL : Device
        {
            public PCL(PCLConfiguration config) : base()
            {

            }

            public override long? ExternalGUID()
            {
                return null;
            }

            public override string ExternalIdentifier()
            {
                return null;
            }

            public override bool Start(bool useThread = true)
            {
                throw new NotImplementedException();
            }

            public override bool Stop()
            {
                throw new NotImplementedException();
            }

            protected override long GetDeviceID()
            {
                throw new NotImplementedException();
            }
        }


        [Test]
        public void ConfiguredDeviceHostShouldRejectDuplicateNames()
        {
            IConfiguredDeviceHostConfiguration hostConfig = new ConfiguredDeviceHostTestConfiguration();

            ConfiguredDeviceHost<Device> devicesHost = new ConfiguredDeviceHost<Device>(hostConfig);

            PCLConfiguration pclConfig1 = new PCLConfiguration();
            PCLConfiguration pclConfig2 = new PCLConfiguration();
            PCLConfiguration pclConfig3 = new PCLConfiguration();
            PCLConfiguration pclConfig4 = new PCLConfiguration();

            pclConfig1.Name = "PCL_XBZBB";
            pclConfig2.Name = "PCL_ZDHH";
            pclConfig3.Name = "PCL_XBZBBYY";
            pclConfig4.Name = "PCL_XBZBB";

            devicesHost.TryAdd("pcl1", pclConfig1).Should().BeTrue();

            devicesHost.TryAdd("pcl1", pclConfig1);

            devicesHost.TryAdd("pcl2", pclConfig2).Should().BeTrue();

            devicesHost.TryAdd("pcl1", pclConfig3).Should().BeFalse();

            Action act = () => devicesHost.TryAdd("pcl3", pclConfig4);

            act.Should().ThrowExactly<ArgumentException>();
         
            Action act2 = () => devicesHost.TryAdd("pcl9238", pclConfig1);

            act2.Should().ThrowExactly<ArgumentException>();

        }
    }
}
