using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    public class ConfiguredDeviceHost<T> where T : Device, new()
    {
        ImmutableDictionary<string, T> _devices;
        ReaderWriterLockSlim _cacheLock;

        public ConfiguredDeviceHost(IConfiguredDeviceHostConfiguration config)
        {
            _devices = ImmutableDictionary.Create<string, T>();
            _cacheLock = new ReaderWriterLockSlim();
        }

        
        private T CreateNewDevice(IDeviceConfiguration deviceConfig)
        {
            if (deviceConfig.Name == null)
                throw new ArgumentException("Device name should not be null in the device configuration.");
            if (_devices.ContainsKey(deviceConfig.Name))
                throw new ArgumentException("Device with this name already exists.");

            T device = new T();

            device.ApplyConfiguration(deviceConfig);

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

            _devices = _devices.Add(deviceName, deviceToAdd);
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

        public bool ReconfigureDevice(T device, IDeviceConfiguration newConfig)
        {
            if (device == null || newConfig == null)
                return false;

            _cacheLock.EnterUpgradeableReadLock();
            try
            {
                _cacheLock.EnterWriteLock();
                device.ApplyConfiguration(newConfig);
                _cacheLock.ExitWriteLock();
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
