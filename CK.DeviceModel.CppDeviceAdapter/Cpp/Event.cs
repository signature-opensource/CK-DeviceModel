using System.Runtime.InteropServices;

namespace CK.DeviceModel.LanguageSpecificDevices.Cpp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        // Conventional usage: stores the number of changed values
        public EventVar Field1;

        // Conventional usage: stores either a value (if changed value is one) or a pointer (IntPtr) to an array of changed values
        public EventVar Field2;

        // Code of the event being sent.
        public byte EventCode;
    }

}
