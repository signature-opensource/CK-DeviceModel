using NUnit.Framework;
using FluentAssertions;
using CK.DeviceModel.LanguageSpecificDevices.Cpp;
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

           
            public override void ApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration config)
            {

            }

            public override void OnTimer(IActivityMonitor monitor, TimeSpan timerSpan)
            {
                throw new NotImplementedException();
            }

            protected override bool StartCppDevice(IntPtr ptr, bool useThread = true)
            {
                throw new NotImplementedException();
            }
        }

        public class PCLConfiguration : IDeviceConfiguration
        {
            public string Name { get; set; }


            public PCLConfiguration()
            {

            }

           
            public IDeviceConfiguration Clone()
            {
                throw new NotImplementedException();
            }

        }

        public class PCL : Device
        {
            public int NumberOfTimesThisHasBeenConfiguration { get; set; } = 0;

            public PCL(PCLConfiguration config) : base(config)
            {
            }

            public override void ApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration config)
            {
                NumberOfTimesThisHasBeenConfiguration++;
            }

            public override long? ExternalGUID()
            {
                return null;
            }

            public override string ExternalIdentifier()
            {
                return null;
            }

            public override void OnTimer(IActivityMonitor monitor, TimeSpan timerSpan)
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


            protected override long GetDeviceID()
            {
                throw new NotImplementedException();
            }
        }


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


        }


    }
}
