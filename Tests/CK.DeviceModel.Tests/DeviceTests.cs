using NUnit.Framework;
using CK.DeviceModel;
using FluentAssertions;
using System.Threading.Tasks;
using CK.Core;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class DeviceTests
    {

        public class CameraConfiguration : IDeviceConfiguration
        {
            public string Name { get; set; }

            public DeviceConfigurationStatus ConfigurationStatus { get; set; }

            public CameraConfiguration(string name, DeviceConfigurationStatus status = DeviceConfigurationStatus.RunnableStarted)
            {
                Name = name;
                ConfigurationStatus = status;
            }

            public IDeviceConfiguration Clone()
            {
                CameraConfiguration config = new CameraConfiguration(Name, ConfigurationStatus);
                return config;
            }
        }

        public class Camera<T> : Device<T> where T : CameraConfiguration
        {

            public Camera(T config) : base(config)
            {

            }


            protected override Task<ApplyConfigurationResult> DoApplyConfigurationAsync(IActivityMonitor monitor, T config, bool? allowRestart)
            {
                throw new System.NotImplementedException();
            }

            protected override Task<bool> DoStartAsync(IActivityMonitor monitor)
            {
                throw new System.NotImplementedException();
            }

            protected override Task DoStopAsync(IActivityMonitor monitor, bool fromConfiguration)
            {
                throw new System.NotImplementedException();
            }
        }

        [Test]
        public void CloneShouldNotHaveTheSameReference()
        {
            CameraConfiguration config = new CameraConfiguration("CameraRGB_XVAJZH_98");
            CameraConfiguration clonedConfig = (CameraConfiguration)config.Clone();
            clonedConfig.Should().NotBe(config);
            clonedConfig.Name.Should().Be(config.Name);
            clonedConfig.ConfigurationStatus.Should().Be(config.ConfigurationStatus);
        }

    
        [Test]
        public void DeviceShouldInitializeProperly()
        {
          

        //    Device<CameraConfiguration> d = new Device<CameraConfiguration>();

        }

    }
}
