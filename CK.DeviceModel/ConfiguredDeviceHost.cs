using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    public class ConfiguredDeviceHost<T> where T : Device
    {
        ImmutableDictionary<string, T> _devices;
        ReaderWriterLockSlim _cacheLock;

        public ConfiguredDeviceHost(IConfiguredDeviceHostConfiguration config)
        {
            _devices = ImmutableDictionary.Create<string, T>();
            _cacheLock = new ReaderWriterLockSlim();
        }

        public bool TryAddDevice(string deviceName, T device)
        {
            if (deviceName == null || device == null)
                return false;

            if (_devices.ContainsKey(deviceName))
            {
                Console.WriteLine("Already exists");
                return false;
            }
           
            _devices = _devices.Add(deviceName, device);
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
