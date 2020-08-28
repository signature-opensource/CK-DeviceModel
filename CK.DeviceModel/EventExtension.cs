using CK.DeviceModel.LanguageSpecificDevices.Cpp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.DeviceModel
{
    public static class EventExtension
    {

        public static int GetNumberOfChangedFields(this Event e)
        {
            return e.Field1.Int0;
        }


        // ------------------------------------------------------------------------------------------------ HANDLING FLOAT ARRAYS ----------------------------------------------------
        public static float[] MarshalToFloatArray(this Event e, int numberOfElements)
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToFloatArray(ptr, numberOfElements);
            return null;
        }


        public static bool MarshalToFloatArray(this Event e, float[] dest, int numberOfElements)
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToFloatArray(ptr, numberOfElements, dest);
            return false;
        }

        public static float[] MarshalToFloatArray(this IntPtr ptr, int numberOfElements)
        {
            float[] dest = new float[numberOfElements];

            if (!MarshalToFloatArray(ptr, numberOfElements, dest))
                return null;

            return dest;
        }

        public static bool MarshalToFloatArray(this IntPtr ptr, int numberOfElements, float[] dest)
        {
            if (dest == null)
                return false;

            if (numberOfElements >= dest.Length || numberOfElements <= 0)
                return false;

            Marshal.Copy(ptr, dest, 0, numberOfElements);

            return true;
        }


        // ---------------------------------------------------------------------------------------- HANDLING DOUBLE ARRAYS -----------------------------------------------------------
        public static double[] MarshalToDoubleArray(this Event e, int numberOfElements)
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToDoubleArray(ptr, numberOfElements);
            return null;
        }


        public static bool MarshalToDoubleArray(this Event e, int numberOfElements, double[] dest)
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToDoubleArray(ptr, numberOfElements, dest);
            return false;
        }

        public static double[] MarshalToDoubleArray(this IntPtr ptr, int numberOfElements)
        {
            double[] dest = new double[numberOfElements];

            if (!MarshalToDoubleArray(ptr, numberOfElements, dest))
                return null;

            return dest;
        }

        public static bool MarshalToDoubleArray(this IntPtr ptr, int numberOfElements, double[] dest)
        {
            if (dest == null)
                return false;

            if (numberOfElements >= dest.Length || numberOfElements <= 0)
                return false;

            Marshal.Copy(ptr, dest, 0, numberOfElements);

            return true;
        }

        // ----------------------------------------------------------------------------------------------------- HANDLING STRUCTS ---------------------------------------------------
        public static ConcreteType? MarshalToStruct<ConcreteType>(this Event e) where ConcreteType : struct, IMappedMemoryZone
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToStruct<ConcreteType>(ptr);

            return null;
        }

        public static bool MarshalToStruct<ConcreteType>(this Event e, ConcreteType dest) where ConcreteType : IMappedMemoryZone
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToStruct(ptr, dest);      
            return false;
        }


        public static ConcreteType? MarshalToStruct<ConcreteType>(this IntPtr ptr) where ConcreteType : struct, IMappedMemoryZone
        {
            ConcreteType marshalled = default;

            if (!MarshalToStruct(ptr, marshalled))
                return null;

            return marshalled;
        }

        public static bool MarshalToStruct<ConcreteType>(this IntPtr ptr, ConcreteType dest) where ConcreteType : IMappedMemoryZone
        {
            if (!dest.GetType().IsSubclassOf(typeof(IMappedMemoryZone)))
            {
                throw new ArgumentException("ConcreteType should be a structure implementing IMappedMemoryZone, not an IMappedMemoryZone itself.");
            }

            try
            {
                dest = Marshal.PtrToStructure<ConcreteType>(ptr);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error" + e);
            }
            return false;
        }
    }
    }
