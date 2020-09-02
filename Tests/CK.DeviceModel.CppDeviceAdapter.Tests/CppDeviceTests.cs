using CK.Core;
using CK.DeviceModel.CppDeviceAdapter.Cpp;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel.CppDeviceAdapter.Tests
{

    public struct YMCA
    {
        public int Y;
        public int M;
        public int C;
        public int A;
    }

    [TestFixture]
    public class CppDeviceTests
    {
        TimeoutTestDevice _dev;


        [Test]
        public void ShouldSendEventCorrectly()
        {
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig(TimeSpan.FromMilliseconds(500), 10);
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration("Timer0001XX", TimeSpan.FromMilliseconds(500), 10, nativeConfig);
          
            _dev = new TimeoutTestDevice(config);

            YMCA test = default;
            test.Y = 8;
            test.M = 8764;
            test.C = -1837;
            test.A = 0;

            Event ymcaEvent = test.ToEvent(25);

            _dev.SendVirtualEventForTests(ymcaEvent);
        }


        [Test]
        public async Task AddCppDeviceShouldWork()
        {
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig(TimeSpan.FromMilliseconds(500), 10);
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration("Timer0001XX", TimeSpan.FromMilliseconds(500), 10, nativeConfig);

            config.Name = "totoch";
            ActivityMonitor monitor = new ActivityMonitor();

            DeviceHostConfiguration<TimeoutTestDeviceConfiguration> hostConfig = new DeviceHostConfiguration<TimeoutTestDeviceConfiguration>();
            hostConfig.Configurations.Add(config);

            ConfiguredDeviceHost<TimeoutTestDevice, TimeoutTestDeviceConfiguration> host = new ConfiguredDeviceHost<TimeoutTestDevice, TimeoutTestDeviceConfiguration>();
            host.Count.Should().Be(0);
            await host.ApplyConfigurationAsync(monitor, hostConfig);
            host.Count.Should().Be(1);
        }

        [Test]
        public async Task StartCppDeviceShouldWork()
        {
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig(TimeSpan.FromMilliseconds(500), 10);
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration("Timer0001XX", TimeSpan.FromMilliseconds(500), 10, nativeConfig);

            config.Name = "totoch";
            ActivityMonitor monitor = new ActivityMonitor();

            DeviceHostConfiguration<TimeoutTestDeviceConfiguration> hostConfig = new DeviceHostConfiguration<TimeoutTestDeviceConfiguration>();
            hostConfig.Configurations.Add(config);

            ConfiguredDeviceHost<TimeoutTestDevice, TimeoutTestDeviceConfiguration> host = new ConfiguredDeviceHost<TimeoutTestDevice, TimeoutTestDeviceConfiguration>();
            host.Count.Should().Be(0);
            await host.ApplyConfigurationAsync(monitor, hostConfig);
            host.Count.Should().Be(1);

            await host[config.Name].StartAsync(monitor);
        }
    }
}
