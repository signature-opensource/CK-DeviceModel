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

    internal struct TestDeviceMemoryZone : IMappedMemoryZone
   {

    }

    internal class TimeoutTestDevice : CppDevice
    {
        public TimeoutTestDevice(TimeoutTestDeviceConfiguration config, TestDeviceMemoryZone memory) : base(config, memory)
        {
            TestDeviceMemoryZone storageForEvent58 = default;

            AddEventProcessing(24, (e) =>
            {
                TestDeviceMemoryZone? zone;
                zone = e.MarshalToStruct<TestDeviceMemoryZone>();
            });

            AddEventProcessing(58, (e) =>
            {
                e.MarshalToStruct(storageForEvent58);
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
            throw new NotImplementedException();
        }


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
            TestDeviceMemoryZone memory = new TestDeviceMemoryZone();

            config.Name = "totoch";

            _dev = new TimeoutTestDevice(config, memory);

            _dev.Start(true);


            Event e = default;
            e.EventCode = 24;

            _dev.SendVirtualEventForTests(e);
        }


        [Test]
        public void EncapsulatedIntPtrShouldNotBeNull()
        {

        }
    }
}
