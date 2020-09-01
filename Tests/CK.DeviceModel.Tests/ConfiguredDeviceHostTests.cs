using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class ConfiguredDeviceHostTests
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

        public class Camera : Device<CameraConfiguration>
        {

            public Camera(CameraConfiguration config) : base(config)
            {

            }


            protected override Task<ApplyConfigurationResult> DoApplyConfigurationAsync(IActivityMonitor monitor, CameraConfiguration config, bool? allowRestart)
            {
                config.ConfigurationStatus = DeviceConfigurationStatus.Runnable;

                return Task.FromResult(ApplyConfigurationResult.Success);
            }

          

            protected override Task<bool> DoStartAsync(IActivityMonitor monitor)
            {
                return Task.FromResult(true);
            }

            protected override Task DoStopAsync(IActivityMonitor monitor, bool fromConfiguration)
            {
                return null;
            }
        }


        [Test]
        public void DeviceHostShouldInitializeProperly()
        {
            CameraConfiguration config = new CameraConfiguration("CameraRGB_XVAJZH_98");
            ConfiguredDeviceHost<Camera, CameraConfiguration> host = new ConfiguredDeviceHost<Camera, CameraConfiguration>();
            ActivityMonitor monitor = new ActivityMonitor();

            DeviceHostConfiguration<CameraConfiguration> hostConfig = new DeviceHostConfiguration<CameraConfiguration>();
            hostConfig.Configurations.Add(config);
            host.Count.Should().Be(0);
            host.ApplyConfigurationAsync(monitor, hostConfig).Wait();          
            host.Count.Should().Be(1);
        }


        [Test]
        public void DeviceHostFindShouldReturnExactReference()
        {
            string cameraModel = "CameraRGB_XVAJZH_98";
            CameraConfiguration config = new CameraConfiguration(cameraModel);
            ConfiguredDeviceHost<Camera, CameraConfiguration> host = new ConfiguredDeviceHost<Camera, CameraConfiguration>();
            ActivityMonitor monitor = new ActivityMonitor();

            DeviceHostConfiguration<CameraConfiguration> hostConfig = new DeviceHostConfiguration<CameraConfiguration>();
            hostConfig.Configurations.Add(config);
            host.Count.Should().Be(0);
            host.ApplyConfigurationAsync(monitor, hostConfig).Wait();
            host.Count.Should().Be(1);

            host[cameraModel].Should().NotBeNull();
            host[cameraModel].Name.Should().Be(cameraModel);
            host[cameraModel + "WRONG"].Should().BeNull();


            Camera cam = host[cameraModel];
            Camera cam2 = host[cameraModel];
            cam.Should().Be(cam2);
        }

        [Test]
        public void DeviceNameAfterAddShouldHaveProperlyConfiguredName()
        {
            CameraConfiguration config = new CameraConfiguration("CameraRGB_XVAJZH_98");
            ConfiguredDeviceHost<Camera, CameraConfiguration> host = new ConfiguredDeviceHost<Camera, CameraConfiguration>();
            ActivityMonitor monitor = new ActivityMonitor();

            DeviceHostConfiguration<CameraConfiguration> hostConfig = new DeviceHostConfiguration<CameraConfiguration>();
            hostConfig.Configurations.Add(config);
            host.Count.Should().Be(0);
            host.ApplyConfigurationAsync(monitor, hostConfig).Wait();
            host.Count.Should().Be(1);
        }
        /*
        
        public static void RunDecrement(object o)
        {
                int count = 99;
                ConfiguredDeviceHost<Device> host = (ConfiguredDeviceHost<Device>)o;
                PCLConfiguration config = new PCLConfiguration();

                while (count >= 0)
                {
                    try
                {
                        config.Name = count.ToString();
                        host.TryAdd(count.ToString(), config);
                    }
                    catch (Exception e)
                    {
                       e.Should().BeOfType(typeof(ArgumentException));
                    }
                    Thread.Sleep(2);
                    count--;
            }
           host.NumberOfDevices.Should().Be(100);

        }
        public static void RunIncrement(object o)
        {
            ConfiguredDeviceHost<Device> host = (ConfiguredDeviceHost<Device>)o;

            int count = 0;
            PCLConfiguration config = new PCLConfiguration();

            while (count < 100)
            {
                try
                {
                    config.Name = count.ToString();
                    host.TryAdd(count.ToString(), config);
                }
                catch (Exception e)
                {
                   e.Should().BeOfType(typeof(ArgumentException));
                }
                Thread.Sleep(5);
                count++;
            }
           host.NumberOfDevices.Should().Be(100);
        }

        public void ReconfigureProcess(ConfiguredDeviceHost<Device> devicesHost, PCLConfiguration configuration)
        {
            Device d;
            for (int i = 0; i < 100; i++)
            {
                d = devicesHost.Find("pcl1");
                d.Should().NotBeNull();
                devicesHost.ReconfigureDevice("pcl1", configuration);
                Thread.Sleep(5);
            }

            ((PCL)devicesHost.Find("pcl1")).NumberOfTimesThisHasBeenConfiguration.Should().BeGreaterOrEqualTo(100);
        }

        [Test]
        public void ReconfigureShouldBeThreadSafe()
        {

            IConfiguredDeviceHostConfiguration hostConfig = new ConfiguredDeviceHostTestConfiguration();
            ConfiguredDeviceHost<Device> devicesHost = new ConfiguredDeviceHost<Device>(hostConfig);

            PCLConfiguration config = new PCLConfiguration();
            config.Name = "PCL_ClientXXX_Number_8789";
            devicesHost.TryAdd("pcl1", config).Should().BeTrue();

            ReconfigureProcess(devicesHost, config);

            Action ReconfigureAction = () =>
            {
                ReconfigureProcess(devicesHost, config);
            };

            Parallel.Invoke(ReconfigureAction, ReconfigureAction, ReconfigureAction, ReconfigureAction);

            ((PCL)devicesHost["pcl1"]).NumberOfTimesThisHasBeenConfiguration.Should().Be(500);
        }


        [Test]
        public void ConfiguredDeviceInitShouldBeThreadSafe()
        {

            IConfiguredDeviceHostConfiguration hostConfig = new ConfiguredDeviceHostTestConfiguration();
            ConfiguredDeviceHost<Device> devicesHost = new ConfiguredDeviceHost<Device>(hostConfig);
            
            Action increase = () =>
            {
                RunIncrement(devicesHost);
            };

            Action decrease = () =>
            {
                RunDecrement(devicesHost);
            };

            Parallel.Invoke(increase, increase, decrease);

            devicesHost.NumberOfDevices.Should().Be(100);
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

            devicesHost.TryAdd("pcl1", pclConfig1).Should().BeFalse();

            devicesHost.TryAdd("pcl2", pclConfig2).Should().BeTrue();

            devicesHost.TryAdd("pcl1", pclConfig3).Should().BeFalse();

            Action act = () => devicesHost.TryAdd("pcl3", pclConfig4);

            act.Should().ThrowExactly<ArgumentException>();
         
            Action act2 = () => devicesHost.TryAdd("pcl9238", pclConfig1);

            act2.Should().ThrowExactly<ArgumentException>();


        }*/





    }
}
