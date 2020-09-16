using CK.Core;
using CK.DeviceModel.CppDeviceAdapter.Cpp;
using FluentAssertions;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CK.DeviceModel.CppDeviceAdapter.Tests
{
    public class TimeoutTestDevice : CppDevice<TimeoutTestDeviceConfiguration>
    {
        

        public TimeoutTestDevice(TimeoutTestDeviceConfiguration config) : base(config)
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

        protected override Task<bool> StartCppDevice(IActivityMonitor monitor, IntPtr ptrToCppDevice)
        {
            return Task.FromResult(StartTimeoutDevice(ptrToCppDevice, true));
        }

        protected override Task StopCppDevice(IActivityMonitor monitor, IntPtr ptrToCppDevice)
        {
            return null;
            //return Task.FromResult(Stop)
        }

        protected override Task<ReconfigurationResult> DoApplyConfigurationAsync(IActivityMonitor monitor, TimeoutTestDeviceConfiguration config, bool? allowRestart)
        {
            return Task.FromResult(ReconfigurationResult.UpdateSucceeded);
        }
    }
}
