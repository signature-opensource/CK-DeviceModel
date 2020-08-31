using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using CK.Core;

namespace CK.DeviceModel
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

    public enum StandardEvent
    {
        error = 0,
        W1 = 1,
        W2 = 2,
        W3 = 3,
        W4 = 4,
        started = 5,
        stopped = 6,
        isstarting = 7,
        isstopping = 8,
        isdestroyed = 9
    };


    public abstract class Device
    {
        public string Name { get; private set; }

        private ProcessChangedValue[] _eventHandlers;

        private long? _GUID;

        public delegate void ProcessChangedValue(Event changedValue);

        public delegate void EventsProcessingCallback(Event e);

        protected EventsProcessingCallback Callback { get; private set; }

        //object _configApplicationLock();

        private void Init(IDeviceConfiguration config)
        {
            Callback = ProcessEvent;
            Name = config.Name;


            // 255 should be enough
            _eventHandlers = new ProcessChangedValue[255];

            // ?
            string externId = ExternalIdentifier();
            if (externId != null)
                Name = externId;

            _GUID = ExternalGUID();
        }


        public Device ( IDeviceConfiguration config )
        {
            Init(config);
        }

        /// <summary>
        /// Allows this device to do any required housekeeping.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="timerSpan">Indicative timer duration.</param>
        public abstract void OnTimer(IActivityMonitor monitor, TimeSpan timerSpan);

        private void Process()
        {

        }

        /// <summary>
        /// Agent starting method. Should be redefined in derived classes, that should start the specific agent. 
        /// </summary>
        /// <returns>True if the agent has been successfully started, false otherwise.</returns>
        public virtual bool Start(bool useThread = true)
        {
            return false;
        }



        /// <summary>
        /// Agent stopping method. Should be redefined in derived classes, that should stop the specific agent.
        /// </summary>
        /// <returns>True if the agent has successfully stopped, false otherwise.</returns>
        public virtual bool Stop()
        {
            return false;
        }

        private IDeviceConfiguration[] _newConf;



        /*
        internal void Reconfigure(IDeviceConfiguration configuration, bool waitForApplication = false)
        {
            Util.InterlockedAdd(ref _newConf, configuration);
            if (waitForApplication)
            {
                lock (_confTrigger)
                {
                    IDeviceConfiguration[] newConf;
                    while (_stopFlag == 0 && (newConf = _newConf) != null && newConf.Contains(configuration))
                        Monitor.Wait(_confTrigger);
                }
            }
        }
        */

#if DEBUG
        /// <summary>
        /// We know that this is within a MRSW-context lock, so we can safely configure.
        /// </summary>
        /// <param name="config">New configuration we want to apply to the device.</param>
        public abstract void ApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration config);

#endif

#if RELEASE
        internal abstract void ApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration config);
#endif

        internal void EnsureApplyConfiguration(IActivityMonitor monitor, IDeviceConfiguration configuration, bool waitForApplication = false)
        {
            if (waitForApplication)
            {

            }
        }

        protected virtual void OnReconfiguring()
        {

        }

        protected virtual void OnReconfigured()
        {

        }

        protected virtual long GetDeviceID()
        {
            return 0;
        }

        // Get serial number
        public virtual string ExternalIdentifier()
        {
            return null;
        }

        public virtual long? ExternalGUID()
        {
            return null;
        }

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
