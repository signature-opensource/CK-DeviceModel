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
                e.MarshalToStruct(storageForEvent58).Should().BeFalse();
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
            return CreateTimeoutAgent(configPtr);
        }

        protected override long GetDeviceID()
        {
            throw new NotImplementedException();
        }

        protected override bool RegisterEventsProcessingCallbackToCppNativeDevice(IntPtr ptrToEncapsulatedCppNativeDevice, IntPtr callbackPtr)
        {
            return RegisterTimeoutAgentCallback(ptrToEncapsulatedCppNativeDevice, callbackPtr);
        }

        [DllImport(MicrOpenCVDllPath)]
        private extern static bool RegisterTimeoutAgentCallback(IntPtr timeoutAgent, IntPtr callbackPtr);

        [DllImport(MicrOpenCVDllPath)]
        private extern static IntPtr CreateTimeoutAgent(IntPtr config);


    }

    [TestFixture]
    public class CppDeviceTests
    {
        TimeoutTestDevice _dev;

        /*
        [SetUp]
        public void Setup()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration();

            _dev = new TimeoutTestDevice(config);

            _dev.Start(true);

        }*/

        [Test]
        public void ShouldSendEventCorrectly()
        {
            TimeoutTestDeviceConfiguration config = new TimeoutTestDeviceConfiguration();
            TestCppDeviceConfig nativeConfig = new TestCppDeviceConfig();

            config.Name = "totoch";

            _dev = new TimeoutTestDevice(config, nativeConfig);

            //_dev.Start(true);


            Event e = default;
            e.EventCode = 24;

            _dev.SendVirtualEventForTests(e);


            Event e2 = default;
            e2.EventCode = 25;
            e2.Field1.Int0 = 1;

            YMCA test = default;
            test.Y = 8;
            test.M = 8764;
            test.C = -1837;
            test.A = 0;

            e2.Field2.IntPtr = Marshal.AllocHGlobal(Marshal.SizeOf(test));
            Marshal.StructureToPtr(test, e2.Field2.IntPtr, true);

            _dev.SendVirtualEventForTests(e2);
        }


        [Test]
        public void EncapsulatedIntPtrShouldNotBeNull()
        {

        }
    }
}
