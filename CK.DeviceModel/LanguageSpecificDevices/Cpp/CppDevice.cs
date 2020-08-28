using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CK.DeviceModel.LanguageSpecificDevices.Cpp
{


    public interface ICppNativeDeviceConfig
    {

    }

    public abstract class CppDevice : Device
    {
        public const string MicrOpenCVDllPath = "CK.SafeDetect.MicrOpenCV.dll";

        /// <summary>
        /// Maximum number of listeners that can subscribe to the current agent.
        /// </summary>   
        private IntPtr _encapsulatedDevice;

        protected CppDevice(ICppDeviceConfiguration config, ICppNativeDeviceConfig cppNativeDeviceConfig) : base(config)
        {
            if (Marshal.SizeOf(config.GetType()) == 0)
                throw new ArgumentException("Configuration cannot be strictly empty.");

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(config.GetType()));
            Marshal.StructureToPtr(config, ptr, true);
            _encapsulatedDevice = CreateCppNativeDevice(ptr);
            if (_encapsulatedDevice == null || _encapsulatedDevice.ToInt32() == 0)
                throw new InvalidOperationException();
            RegisterEventsProcessingCallbackToCppNativeDevice(Callback);
        }

        /// <summary>
        /// Should call a method of prototype: "private extern static IntPtr Create[CPP_CLASSNAME](IntPtr configPtr)"
        /// On the C++ side, this Create[NAME]Device should have the prototype: "extern "C" [CPP_CLASSNAME] *Create[CPP_CLASSNAME]([CPP_CLASSNAME + 'Config'] *config)."
        /// </summary>
        /// <param name="configPtr">Pointer to the configuration of the Cpp native device.</param>
        /// <returns>IntPtr returned by the Create[NAME]Device function.</returns>
        protected abstract IntPtr CreateCppNativeDevice(IntPtr configPtr);

        /// <summary>
        /// Should call a method of prototpye: "private extern static bool Register[CPP_CLASSNAME]Callback(IntPtr devicePtr, IntPtr callbackPtr)."
        /// On the C++ side, this RegisterCallback should have prototype: "extern "C" bool Register[CPP_CLASSNAME]Callback([CPP_CLASSNAME] *device, CallbackFuncPtr *callback)"
        /// </summary>
        /// <param name="ptrToEncapsulatedCppNativeDevice"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private bool RegisterEventsProcessingCallbackToCppNativeDevice(EventsProcessingCallback callback)
        {
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);

            if (_encapsulatedDevice == null || _encapsulatedDevice.ToInt32() == 0)
                throw new NullReferenceException("EncapsulatedDevice is null or has not be properly allocated: are you sure you didn't mess up?");

            return RegisterEventsProcessingCallbackToCppNativeDevice(_encapsulatedDevice, ptr);
        }

        protected abstract bool RegisterEventsProcessingCallbackToCppNativeDevice(IntPtr ptrToEncapsulatedCppNativeDevice, IntPtr callbackPtr);

    }
}
