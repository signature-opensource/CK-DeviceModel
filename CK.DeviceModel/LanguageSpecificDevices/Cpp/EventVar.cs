using System;
using System.Runtime.InteropServices;

namespace CK.DeviceModel
{
    [StructLayout(LayoutKind.Explicit)]
    public struct EventVar
    {
        [FieldOffset(0)]
        public IntPtr IntPtr;

        [FieldOffset(0)]
        public byte Byte0;
        [FieldOffset(1)]
        public byte Byte1;
        [FieldOffset(2)]
        public byte Byte2;
        [FieldOffset(3)]
        public byte Byte3;
        [FieldOffset(4)]
        public byte Byte4;
        [FieldOffset(5)]
        public byte Byte5;
        [FieldOffset(6)]
        public byte Byte6;
        [FieldOffset(7)]
        public byte Byte7;

        [FieldOffset(0)]
        public sbyte SByte0;
        [FieldOffset(1)]
        public sbyte SByte1;
        [FieldOffset(2)]
        public sbyte SByte2;
        [FieldOffset(3)]
        public sbyte SByte3;
        [FieldOffset(4)]
        public sbyte SByte4;
        [FieldOffset(5)]
        public sbyte SByte5;
        [FieldOffset(6)]
        public sbyte SByte6;
        [FieldOffset(7)]
        public sbyte SByte7;

        [FieldOffset(0)]
        public double Double;

        [FieldOffset(0)]
        public long Long;

        [FieldOffset(0)]
        public ulong ULong;

        [FieldOffset(0)]
        public int Int0;
        [FieldOffset(4)]
        public int Int1;

        [FieldOffset(0)]
        public uint UInt0;
        [FieldOffset(4)]
        public uint UInt1;

        [FieldOffset(0)]
        public float Float0;
        [FieldOffset(4)]
        public float Float1;

        [FieldOffset(0)]
        public short Short0;
        [FieldOffset(2)]
        public short Short1;
        [FieldOffset(4)]
        public short Short2;
        [FieldOffset(6)]
        public short Short3;

        [FieldOffset(0)]
        public ushort UShort0;
        [FieldOffset(2)]
        public ushort UShort1;
        [FieldOffset(4)]
        public ushort UShort2;
        [FieldOffset(6)]
        public ushort UShort3;

    }

}
