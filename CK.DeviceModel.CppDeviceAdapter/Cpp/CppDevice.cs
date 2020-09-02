using CK.Core;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CK.DeviceModel.CppDeviceAdapter.Cpp
{

    public abstract class CppDevice<TConfiguration> : Device<TConfiguration> where TConfiguration : ICppDeviceConfiguration  
    {
        public const string MicrOpenCVDllPath = "CK.SafeDetect.MicrOpenCV.dll";

        private ProcessChangedValue[] _eventHandlers;

        public delegate void ProcessChangedValue(Event changedValue);

        public delegate void EventsProcessingCallback(Event e);

        protected EventsProcessingCallback Callback;


        /// <summary>
        /// Maximum number of listeners that can subscribe to the current agent.
        /// </summary>   
        private IntPtr _encapsulatedDevice;

        protected CppDevice(TConfiguration config) : base(config)
        {
           ICppNativeDeviceConfig cppDeviceConfig = config.NativeDeviceConfig;
            if (cppDeviceConfig == null) throw new ArgumentException("Cpp device configuration cannot be null.");
            if (cppDeviceConfig.GetType() == typeof(ICppNativeDeviceConfig))
                throw new ArgumentException("CppDeviceConfig should be a struct inheriting from ICppNativeDeviceConfig, not an ICppNativeDeviceConfig itself.");
            if (Marshal.SizeOf(cppDeviceConfig.GetType()) == 0)
                throw new ArgumentException("Memory zone cannot be strictly empty.");

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(cppDeviceConfig.GetType()));
            Marshal.StructureToPtr(cppDeviceConfig, ptr, true);
            _encapsulatedDevice = CreateCppNativeDevice(ptr);
            _eventHandlers = new ProcessChangedValue[255];
            if (_encapsulatedDevice == null)
                throw new InvalidOperationException("Encapsulated device shouldn't be null or have invalid adress: make sure CreateCppNativeDevice creates properly and returns a non-null value.");

        }

        protected abstract Task<bool> StartCppDevice(IActivityMonitor monitor, IntPtr ptrToCppDevice);

        protected abstract Task StopCppDevice(IActivityMonitor monitor, IntPtr ptrToCppDevice);

        protected override sealed Task<bool> DoStartAsync(IActivityMonitor monitor)
        {
            if (Callback == null)
            {
                Callback = ProcessEvent;
                RegisterEventsProcessingCallbackToCppNativeDevice(Callback);
            }
            return StartCppDevice(monitor, _encapsulatedDevice);
        }

        protected override sealed Task DoStopAsync(IActivityMonitor monitor, bool fromConfiguration)
        {
            return StopCppDevice(monitor, _encapsulatedDevice);
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
            Delegate del = callback;

            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(del);

            if (_encapsulatedDevice == null)
                throw new NullReferenceException("EncapsulatedDevice is null or has not be properly allocated: are you sure you didn't mess up?");

            return RegisterEventsProcessingCallbackToCppNativeDevice(_encapsulatedDevice, ptr);
        }

        protected abstract bool RegisterEventsProcessingCallbackToCppNativeDevice(IntPtr ptrToEncapsulatedCppNativeDevice, IntPtr callbackPtr);



    
        protected void ProcessEvent(Event e)
        {
            // Getting the number of changed fields
            byte eventID = (byte)(Enum.GetValues(typeof(StandardEvent)).Length + e.EventCode);
            if (eventID < _eventHandlers.Length)
                _eventHandlers[eventID](e);
        }

#if DEBUG
        public void SendVirtualEventForTests(Event e)
        {
            ProcessEvent(e);
        }
#endif

        protected bool AddStandardEventProcessing(StandardEvent e, ProcessChangedValue OnChange)
        {
            return AddStandardEventProcessing((byte)e, OnChange);
        }

        private bool AddStandardEventProcessing(byte eventID, ProcessChangedValue OnChange)
        {
            if (eventID > Enum.GetValues(typeof(StandardEvent)).Length)
                return false;

            _eventHandlers[eventID] = OnChange;

            return true;
        }

        protected bool AddEventProcessing(byte eventId, ProcessChangedValue OnChange)
        {
            byte eventID = (byte)(Enum.GetValues(typeof(StandardEvent)).Length + eventId);
            if (eventID < Enum.GetValues(typeof(StandardEvent)).Length)
                return false;

            _eventHandlers[eventID] = OnChange;

            return true;
        }
    }
}
