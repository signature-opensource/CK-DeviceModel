using CK.Core;
using CK.DeviceModel.LanguageSpecificDevices.Cpp;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;

namespace CK.DeviceModel.Tests.LanguageSpecificDevices.Cpp
{
    internal class TimeoutTestDeviceConfiguration : ICppDeviceConfiguration
    {
        public string Name { get ; set ; }

        public IDeviceConfiguration Clone()
        {
            throw new NotImplementedException();
        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TestCppDeviceConfig : ICppNativeDeviceConfig
    {
        public int i;
    }

    public struct YMCA
    {
        public int Y;
        public int M;
        public int C;
        public int A;
    }

    internal class TimeoutTestDevice : CppDevice
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
        }

        public override long? ExternalGUID()
        {
            return null;
        }

        public override string ExternalIdentifier()
        {
            return null;
        }


        protected override bool StartCppDevice(IntPtr ptr, bool useThread = true)
        {
            return StartTimeoutDevice(ptr, useThread);
        }

        public override bool Stop()
        {
            throw new NotImplementedException();
        }

        protected override IntPtr CreateCppNativeDevice(IntPtr configPtr)
        {
            return CreateTimeoutDevice(configPtr);
        }

        protected override long GetDeviceID()
        {
            throw new NotImplementedException();
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


        public override void OnTimer(IActivityMonitor monitor, TimeSpan timerSpan)
        {
            throw new NotImplementedException();
        }

        public override void ApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration config)
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
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration();
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
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration();
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig();

            config.Name = "totoch";

            _dev = new TimeoutTestDevice(config, nativeConfig);

            _dev.Start(true).Should().BeTrue();
        }
    }
}
