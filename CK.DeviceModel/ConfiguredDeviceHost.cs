using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    public class ConfiguredDeviceHost<T> where T : Device
    {
        ImmutableDictionary<string, T> _devices;
        ReaderWriterLockSlim _cacheLock;
        SpinLock _sl;
        DeviceDispatcherSink<T> _deviceConfigDispatcher;


        object _addTransactionLock;


        public ConfiguredDeviceHost(IActivityMonitor monitor, IConfiguredDeviceHostConfiguration config)
        {
            _devices = ImmutableDictionary.Create<string, T>();
            _cacheLock = new ReaderWriterLockSlim();
            _sl = new SpinLock(true);
            _deviceConfigDispatcher = new DeviceDispatcherSink<T>(monitor, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), null, null);
            _addTransactionLock = new object();
        }

        public int NumberOfDevices => _devices.Count;

        private T CreateNewDevice(IDeviceConfiguration deviceConfig)
        {
            if (deviceConfig.Name == null)
                throw new ArgumentException("Device name should not be null in the device configuration.");
            if (_devices.ContainsKey(deviceConfig.Name))
                throw new ArgumentException("Device with this name already exists.");

            string configClassName = deviceConfig.GetType().GetTypeInfo().FullName;

            if (!configClassName.EndsWith("Configuration"))
                throw new ArgumentException("deviceConfig's class name should be XXXXXConfiguration.");
            
            string deviceClassName = deviceConfig.GetType().AssemblyQualifiedName.Replace("Configuration,", ",");

            Type deviceType = Type.GetType(deviceClassName);
            if (deviceType == null)
                throw new ArgumentException("Could not find matching device class.");

            if (!(deviceType.IsSubclassOf(typeof(T)) || deviceType == typeof(T)))
                throw new ArgumentException("Device to instantiate should either be of type T or a Subclass of T.");

            T device = (T)Activator.CreateInstance(deviceType, new[] { deviceConfig } );
      
            return device;
        }

        public bool TryAdd(string deviceUserSetName, IDeviceConfiguration deviceConfig, int maxNumberOfTries = int.MaxValue)
        {
            lock (_addTransactionLock)
            {
                if (deviceUserSetName == null || deviceConfig == null || deviceConfig.Name == null)
                    return false;

                if (_devices.ContainsKey(deviceUserSetName))
                {
                    Console.WriteLine("Already exists");
                    return false;
                }

                if (_devices.Values.Any(x => x.Name == deviceConfig.Name))
                    throw new ArgumentException("Duplicate configuration name exception! Please make sure this configuration has a unique name.");

                T deviceToAdd = CreateNewDevice(deviceConfig);

                return TryAdd(deviceUserSetName, deviceToAdd, maxNumberOfTries);
            }

        }

        internal bool TryAdd(string deviceUserSetName, T device, int maxNumberOfTries)
        {
            var spin = new SpinWait();
            int currentTry = -1;
            ActivityMonitor internalMonitor = new ActivityMonitor();
            while (++currentTry < maxNumberOfTries)
            {
                if (_devices.ContainsKey(deviceUserSetName)) return false;
                if (_devices.ContainsValue(device)) return false;
                if (_devices.Values.Any(x => x.Name == device.Name)) return false;

                var devices = _devices;
                if (devices == null) return false;

                // devices or _devices?
                var newDevicesDic = _devices.Add(deviceUserSetName, device);
                Interlocked.CompareExchange(ref _devices, newDevicesDic, devices);
                if (_devices == newDevicesDic)
                    break;
                spin.SpinOnce();
            }
            return currentTry < maxNumberOfTries;
        }

        public bool RenameDevice(string newDeviceName, T device)
        {
            if (device == null)
                return false;
            
            _cacheLock.EnterUpgradeableReadLock();
            try
            {
                _cacheLock.EnterWriteLock();
                _devices.Remove(device.Name);
                _cacheLock.ExitWriteLock();
            }
            finally
            {
                _cacheLock.ExitUpgradeableReadLock();
            }
            return true;
        }

        public bool RenameDevice(string newDeviceName, string oldDeviceName)
        {
            T device;
            if (!_devices.TryGetValue(oldDeviceName, out device))
                return false;
            return RenameDevice(newDeviceName, device);
        }

        public T this[string deviceName]
        {
            get
            {
                return Find(deviceName);
            }
            set
            {
                RenameDevice(deviceName, value);
            }
        }

        public T Find(string deviceName)
        {
            T d = null;
            _devices.TryGetValue(deviceName, out d);
            return d;
        }

        private bool ReconfigureDevice(T device, IDeviceConfiguration newConfig, bool waitForApplication)
        {
            ActivityMonitor internalMonitor = new ActivityMonitor();
            if (device == null || newConfig == null)
                return false;

            bool gotLock = false;
            _cacheLock.EnterUpgradeableReadLock();
            try
            {
                _sl.Enter(ref gotLock);
                _cacheLock.EnterWriteLock();
                device.ApplyConfiguration(internalMonitor, newConfig);
                _cacheLock.ExitWriteLock();
            }
            finally
            {
                _cacheLock.ExitUpgradeableReadLock();
                if (gotLock) _sl.Exit();
            }
            return true;
        }

        public bool ReconfigureDevice(string deviceName, IDeviceConfiguration newConfig, bool waitForApplication = true)
        {
            T device = Find(deviceName);
            if (device == null)
                return false;
            return ReconfigureDevice(device, newConfig, waitForApplication);
        }
    }
}
