using CK.Core;
using CK.DeviceModel.LanguageSpecificDevices.Cpp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.DeviceModel.LanguageSpecificDevices.Cpp
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
            if (e.Field1.Int0 <= 0)
                return null;
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToFloatArray(ptr, numberOfElements);
            return null;
        }


        public static bool MarshalToFloatArray(this Event e, float[] dest, int numberOfElements)
        {
            if (e.Field1.Int0 <= 0)
                return false;
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
            if (e.Field1.Int0 <= 0)
                return null;
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToDoubleArray(ptr, numberOfElements);
            return null;
        }


        public static bool MarshalToDoubleArray(this Event e, int numberOfElements, double[] dest)
        {
            if (e.Field1.Int0 <= 0)
                return false;
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
        public static ConcreteType? MarshalToStruct<ConcreteType>(this Event e) where ConcreteType : struct
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToStruct<ConcreteType>(ptr);

            return null;
        }

        public static bool MarshalToStruct<ConcreteType>(this Event e, ref ConcreteType dest) where ConcreteType : struct
        {
            if (e.Field2.IntPtr is IntPtr ptr)
                return MarshalToStruct(ptr, ref dest);      
            return false;
        }


        public static ConcreteType? MarshalToStruct<ConcreteType>(this IntPtr ptr) where ConcreteType : struct
        {
            ConcreteType marshalled = default;

            if (!MarshalToStruct(ptr, ref marshalled))
                return null;

            return marshalled;
        }

        public static bool MarshalToStruct<ConcreteType>(this IntPtr ptr, ref ConcreteType dest) where ConcreteType : struct
        {
            if (ptr == null)
                return false;
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

        public static void DisposeStructureEvent<T>(this Event e) where T : struct
        {
            int numberOfStructs = e.GetNumberOfChangedFields();

            if (numberOfStructs <= 0)
                throw new ArgumentException("Cannot free unmanaged memory: there should be at least one element to free.");
            if (e.Field2.IntPtr == null)
                throw new ArgumentException("Cannot free unmanaged memory: pointer to unmanaged memory should not be null.");

            Marshal.DestroyStructure<T>(e.Field2.IntPtr);
            for (int i = 0; i < numberOfStructs; i++)
            {
                if (e.Field2.IntPtr != null)
                    Marshal.FreeHGlobal(e.Field2.IntPtr);
            }
        }

        public static T[] MarshalStructArray<T>(this Event e) where T: struct
        {
            int numberOfElements = e.GetNumberOfChangedFields();
            if (numberOfElements <= 0)
                return null;

            T[] res = new T[numberOfElements];

            try
            {
                IntPtr[] dest = new IntPtr[numberOfElements];
                Marshal.Copy(e.Field2.IntPtr, dest, 0, numberOfElements);
                int i = 0;
                foreach (IntPtr ptr in dest)
                {
                    res[i++] = Marshal.PtrToStructure<T>(ptr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
                return null;
            }
            return res;
        }

        public static Event ToEvent<T>(this IReadOnlyCollection<T> structures, byte eventCode) where T : struct
        {
            Event e = default;
            e.EventCode = eventCode;
            e.Field1.Int0 = structures.Count;

            e.Field2.IntPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)) * structures.Count);

            IntPtr[] tmp = new IntPtr[structures.Count];
            int i = 0;
            foreach (T structure in structures)
            {
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)));
                Marshal.StructureToPtr(structure, ptr, true);
                tmp[i++] = ptr;
            }
            Marshal.Copy(tmp, 0, e.Field2.IntPtr, structures.Count);
            /*
            foreach (IntPtr t in tmp)
            {
                Marshal.FreeHGlobal(t);
            }*/

            return e;
        }

        public static Event ToEvent<T>(this T structure, byte eventCode) where T : struct
        {
            Event e = new Event();
            e.EventCode = eventCode;
            e.Field1.Int0 = 1;
            e.Field2.IntPtr = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
            Marshal.StructureToPtr(structure, e.Field2.IntPtr, true);
            
            return e;
        }
    }
    }
