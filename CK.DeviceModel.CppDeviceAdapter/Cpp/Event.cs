using System.Runtime.InteropServices;

namespace CK.DeviceModel.CppDeviceAdapter.Cpp
{
    /// <summary>
    /// Event structure sent by the C++.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        /// <summary>
        /// Conventional usage: stores the number of values we are processing.
        /// </summary>
        public EventVar Field1;

        /// <summary>
        /// Conventional usage: stores either a value (if changed value is one) 
        /// or a pointer (IntPtr) to an array of values.
        /// </summary>
        public EventVar Field2;

        /// <summary>
        /// Code of the event being sent, to distinguish it from other events.
        /// Should be unique in the context of the device.
        /// </summary>
        public byte EventCode;
    }

}
