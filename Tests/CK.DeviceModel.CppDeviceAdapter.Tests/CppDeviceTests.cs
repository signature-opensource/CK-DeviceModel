using CK.Core;
using CK.DeviceModel.LanguageSpecificDevices.Cpp;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests.LanguageSpecificDevices.Cpp
{
    internal class TimeoutTestDeviceConfiguration : ICppDeviceConfiguration
    {
        public string Name { get ; set ; }
        public DeviceConfigurationStatus ConfigurationStatus { get; set; }

        int _maxCount;

        TimeSpan _cycleDuration;

        public IDeviceConfiguration Clone()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration(Name, _cycleDuration, _maxCount);
            return config;
        }

        public TimeoutTestDeviceConfiguration(string name, TimeSpan cycleDuration, int maxCount)
        {
            Name = name;
            _cycleDuration = TimeSpan.FromMilliseconds(cycleDuration.TotalMilliseconds);
            _maxCount = maxCount;
        }
    }


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
    }

    public struct YMCA
    {
        public int Y;
        public int M;
        public int C;
        public int A;
    }

    internal class TimeoutTestDevice : CppDevice<TimeoutTestDeviceConfiguration>
    {


        public TimeoutTestDevice(TimeoutTestDeviceConfiguration config, TestCppDeviceConfig nativeDeviceConfig) : base(config, nativeDeviceConfig)
        {
            YMCA storageForEvent58 = default;

            AddEventProcessing(24, (e) =>
            {
                YMCA? zone;
                zone = e.MarshalToStruct<YMCA>();
                zone.Should().BeNull();
            });

            AddEventProcessing(25, (e) =>
            {
                YMCA? zone;
                zone = e.MarshalToStruct<YMCA>();
                zone.Should().NotBeNull();
                YMCA val = zone.Value;
                val.Y.Should().Be(8);
                val.M.Should().Be(8764);
                val.C.Should().Be(-1837);
                val.A.Should().Be(0);

            });
            AddEventProcessing(58, (e) =>
            {
                e.MarshalToStruct(ref storageForEvent58).Should().BeFalse();
            });

            AddEventProcessing(66, (e) =>
            {
                float[] dest = new float[100];
                e.MarshalToFloatArray(dest, 10).Should().BeTrue();
                dest.Should().NotBeNull();
                dest.Should().NotBeEmpty();
                dest[0].Should().BeGreaterThan(0);
            });
        }


        protected override bool StartCppDevice(IntPtr ptr, bool useThread = true)
        {
            return StartTimeoutDevice(ptr, useThread);
        }

        
        protected override IntPtr CreateCppNativeDevice(IntPtr configPtr)
        {
            return CreateTimeoutDevice(configPtr);
        }

        protected override bool RegisterEventsProcessingCallbackToCppNativeDevice(IntPtr ptrToEncapsulatedCppNativeDevice, IntPtr callbackPtr)
        {
            return RegisterTimeoutDeviceCallback(ptrToEncapsulatedCppNativeDevice, callbackPtr);
        }


        [DllImport(MicrOpenCVDllPath)]
        private extern static bool StartTimeoutDevice(IntPtr timeoutDevice, bool useThread);

        [DllImport(MicrOpenCVDllPath)]
        private extern static bool RegisterTimeoutDeviceCallback(IntPtr timeoutDevice, IntPtr callbackPtr);

        [DllImport(MicrOpenCVDllPath)]
        private extern static IntPtr CreateTimeoutDevice(IntPtr config);


        protected override Task<ApplyConfigurationResult> DoApplyConfigurationAsync(IActivityMonitor monitor, TimeoutTestDeviceConfiguration config, bool? allowRestart)
        {
            throw new NotImplementedException();
        }

        protected override Task<bool> DoStartAsync(IActivityMonitor monitor)
        {
            throw new NotImplementedException();
        }

        protected override Task DoStopAsync(IActivityMonitor monitor, bool fromConfiguration)
        {
            throw new NotImplementedException();
        }
    }

    [TestFixture]
    public class CppDeviceTests
    {
        TimeoutTestDevice _dev;


        [Test]
        public void ShouldSendEventCorrectly()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration("Timer0001XX", TimeSpan.FromMilliseconds(500), 10);
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig();

            config.Name = "totoch";

            _dev = new TimeoutTestDevice(config, nativeConfig);

            Event e = default;
            e.EventCode = 24;

            _dev.SendVirtualEventForTests(e);



            YMCA test = default;
            test.Y = 8;
            test.M = 8764;
            test.C = -1837;
            test.A = 0;

            Event ymcaEvent = test.ToEvent(25);

            _dev.SendVirtualEventForTests(ymcaEvent);
        }


        [Test]
        public void StartShouldWork()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration("Timer0001XX", TimeSpan.FromMilliseconds(500), 10);
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig(TimeSpan.FromMilliseconds(500), 10);

            config.Name = "totoch";

            _dev = new TimeoutTestDevice(config, nativeConfig);

            ActivityMonitor monitor = new ActivityMonitor();

            _dev.StartAsync(monitor);

            Thread.Sleep(10000);
        }
    }
}
