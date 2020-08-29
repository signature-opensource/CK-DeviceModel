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

        public ConfiguredDeviceHost(IConfiguredDeviceHostConfiguration config)
        {
            _devices = ImmutableDictionary.Create<string, T>();
            _cacheLock = new ReaderWriterLockSlim();
            _sl = new SpinLock(true);
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

        public bool TryAdd(string deviceName, IDeviceConfiguration deviceConfig)
        {
            if (deviceName == null || deviceConfig == null || deviceConfig.Name == null)
                return false;

            if (_devices.ContainsKey(deviceName))
            {
                Console.WriteLine("Already exists");
                return false;
            }

            if (_devices.Values.Any(x => x.Name == deviceConfig.Name))
                throw new ArgumentException("Duplicate configuration name exception! Please make sure this configuration has a unique name.");

            T deviceToAdd = CreateNewDevice(deviceConfig);

            var h = _devices;
            h = h.Add(deviceName, deviceToAdd);
            Interlocked.Exchange(ref _devices, h);
            return true;
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

        private bool ReconfigureDevice(T device, IDeviceConfiguration newConfig)
        {
            if (device == null || newConfig == null)
                return false;

            bool gotLock = false;
            _cacheLock.EnterUpgradeableReadLock();
            try
            {
                _sl.Enter(ref gotLock);
                _cacheLock.EnterWriteLock();
                device.Reconfigure(newConfig);
                _cacheLock.ExitWriteLock();
                _sl.Exit();
            }
            finally
            {
                _cacheLock.ExitUpgradeableReadLock();
            }
            return true;
        }

        public bool ReconfigureDevice(string deviceName, IDeviceConfiguration newConfig)
        {
            T device = Find(deviceName);
            if (device == null)
                return false;
            return ReconfigureDevice(device, newConfig);
        }
    }
}
