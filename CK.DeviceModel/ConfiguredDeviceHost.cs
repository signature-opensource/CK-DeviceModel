using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public class ConfiguredDeviceHost<T,TConfiguration> : IDeviceHost where T : Device<TConfiguration> where TConfiguration : IDeviceConfiguration
    {
        readonly SemaphoreSlim _lock;
        Dictionary<string, T> _devices;

        public ConfiguredDeviceHost()
        {
            _lock = new SemaphoreSlim( 0, 1 );
            _devices = new Dictionary<string, T>();
        }

        public int Count => _devices.Count;

        //private T CreateNewDevice(IDeviceConfiguration deviceConfig)
        //{
        //    if (deviceConfig.Name == null)
        //        throw new ArgumentException("Device name should not be null in the device configuration.");
        //    if (_devices.ContainsKey(deviceConfig.Name))
        //        throw new ArgumentException("Device with this name already exists.");

        //    string configClassName = deviceConfig.GetType().GetTypeInfo().FullName;

        //    if (!configClassName.EndsWith("Configuration"))
        //        throw new ArgumentException("deviceConfig's class name should be XXXXXConfiguration.");
            
        //    string deviceClassName = deviceConfig.GetType().AssemblyQualifiedName.Replace("Configuration,", ",");

        //    Type deviceType = Type.GetType(deviceClassName);
        //    if (deviceType == null)
        //        throw new ArgumentException("Could not find matching device class.");

        //    if (!(deviceType.IsSubclassOf(typeof(T)) || deviceType == typeof(T)))
        //        throw new ArgumentException("Device to instantiate should either be of type T or a Subclass of T.");

        //    T device = (T)Activator.CreateInstance(deviceType, new[] { deviceConfig } );
      
        //    return device;
        //}

        //public bool TryAdd(string deviceUserSetName, IDeviceConfiguration deviceConfig)
        //{
        //    lock (_addTransactionLock)
        //    {
        //        if (deviceUserSetName == null || deviceConfig == null || deviceConfig.Name == null)
        //            return false;

        //        if (_devices.ContainsKey(deviceUserSetName))
        //        {
        //            Console.WriteLine("Already exists");
        //            return false;
        //        }

        //        if (_devices.Values.Any(x => x.Name == deviceConfig.Name))
        //            throw new ArgumentException("Duplicate configuration name exception! Please make sure this configuration has a unique name.");

        //        T deviceToAdd = CreateNewDevice(deviceConfig);

        //        _devices = _devices.Add(deviceUserSetName, deviceToAdd);

        //        //return TryAdd(deviceUserSetName, deviceToAdd);
        //    }
        //    return true;
        //}

        //internal bool TryAdd(string deviceUserSetName, T device, int maxNumberOfTries)
        //{
        //    var spin = new SpinWait();
        //    int currentTry = -1;
        //    ActivityMonitor internalMonitor = new ActivityMonitor();
        //    while (++currentTry < maxNumberOfTries)
        //    {
        //        if (_devices.ContainsKey(deviceUserSetName)) return false;
        //        if (_devices.ContainsValue(device)) return false;
        //        if (_devices.Values.Any(x => x.Name == device.Name)) return false;

        //        var devices = _devices;
        //        if (devices == null) return false;

        //        // devices or _devices?
        //        var newDevicesDic = _devices.Add(deviceUserSetName, device);
        //        Interlocked.CompareExchange(ref _devices, newDevicesDic, devices);
        //        if (_devices == newDevicesDic)
        //            break;
        //        spin.SpinOnce();
        //    }
        //    return currentTry < maxNumberOfTries;
        //}

        //public bool RenameDevice(string newDeviceName, T device)
        //{
        //    if (device == null)
        //        return false;
            
        //    _cacheLock.EnterUpgradeableReadLock();
        //    try
        //    {
        //        _cacheLock.EnterWriteLock();
        //        _devices.Remove(device.Name);
        //        _cacheLock.ExitWriteLock();
        //    }
        //    finally
        //    {
        //        _cacheLock.ExitUpgradeableReadLock();
        //    }
        //    return true;
        //}

        //public bool RenameDevice(string newDeviceName, string oldDeviceName)
        //{
        //    T device;
        //    if (!_devices.TryGetValue(oldDeviceName, out device))
        //        return false;
        //    return RenameDevice(newDeviceName, device);
        //}

        public T? this[string deviceName] => Find( deviceName );

        public T? Find(string deviceName) => _devices.GetValueOrDefault(deviceName);



        public async Task<bool> ApplyConfigurationAsync(IActivityMonitor monitor, IConfiguredDeviceHostConfiguration<TConfiguration> configuration, bool allowEmptyConfiguration = false )
        {
            var safeConfig = CloneAndCheckConfig(monitor, configuration, allowEmptyConfiguration);
            if (safeConfig == null) return false;
            bool success = true;
            using (monitor.OpenInfo($"Reconfiguring '{GetType().Name}'."))
            {
                await _lock.WaitAsync();
                try
                {
                    var newDevices = new Dictionary<string, T>(_devices);
                    
                    if (!await OnBeforeApplyConfiguration(monitor, newDevices)) return false;

                    var existingDevices = new HashSet<string>( newDevices.Keys );
                    foreach( var c in safeConfig.Configurations )
                    {
                        if( !newDevices.TryGetValue( c.Name, out var d ) )
                        {
                            d = CreateDevice(monitor, c);
                            Debug.Assert(d == null || d.Name == c.Name);
                            if (d == null)
                            {
                                success = false;
                                continue;
                            }
                            d._host = this;
                            newDevices.Add(c.Name, d);
                        }
                        else
                        {
                            existingDevices.Remove(c.Name);
                        }
                        var r = await d.ApplyConfigurationAsync(monitor, c, allowRestart: null);
                        success &= r == ApplyConfigurationResult.Success;
                    }
                    foreach ( var noMore in existingDevices )
                    {
                        var d = newDevices[noMore];
                        await OnRemoveAsync(monitor, d);
                        newDevices.Remove(noMore);
                    }
                    _devices = newDevices;
                }
                catch (Exception ex)
                {
                    monitor.Error(ex);
                    success = false;
                }
                finally
                {
                    _lock.Release();
                }
            }
            return success;
        }

        protected virtual Task<bool> OnBeforeApplyConfiguration(IActivityMonitor monitor, Dictionary<string, T> newDevices) => Task.FromResult(true);

        protected virtual T? CreateDevice(IActivityMonitor monitor, IDeviceConfiguration config)
        {
            /// ....
            return null;
        }


        /// <summary>
        /// Called when a device must be removed (its configuration disappeared).
        /// By default, calls <see cref="Device{TConfiguration}.StopAsync(IActivityMonitor, bool)"/> on the device and <see cref="Device{TConfiguration}.Destroy(IActivityMonitor)"/>.
        /// There is no way to prevent the device to be removed when its configuration disappeared and this is by design.
        /// Note that this base method MUST be called when overriding this behavior.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="device">The device that will be removed.</param>
        /// <returns>The awaitable.</returns>
        protected virtual async Task OnRemoveAsync(IActivityMonitor monitor, T device)
        {
            await device.DoStopAsync(monitor, true);
            device._host = null;
            device.Destroy(monitor);
        }

        IConfiguredDeviceHostConfiguration<TConfiguration>? CloneAndCheckConfig(IActivityMonitor monitor, IConfiguredDeviceHostConfiguration<TConfiguration> configuration, bool allowEmptyConfiguration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var safe = configuration.Clone();
            var dedup = new HashSet<string>();
            bool success = true; 
            int idx = 0;
            foreach( var c in safe.Configurations )
            {
                ++idx;
                var name = c.Name;
                if (String.IsNullOrWhiteSpace(name))
                {
                    monitor.Error($"Configuration n°{idx}: Configuration name is invalid.");
                    success = false;
                }
                if ( !dedup.Add( c.Name ) )
                {
                    monitor.Error($"Duplicate configuration found: '{c.Name}'. Configuration names must be unique.");
                    success = false;
                }
            }
            if( idx == 0 && !allowEmptyConfiguration )
            {
                monitor.Error($"Empty configuration is not allowed.");
                success = false;
            }
            return success && CheckConfiguration(monitor, safe) ? safe : null;
        }

        /// <summary>
        /// Called on a cloned and already valid configuration to allow more detailed checks.
        /// </summary>
        /// <param name="monitor">The monitor to use: any error must be logged.</param>
        /// <param name="safe">The cloned configuration.</param>
        /// <returns>True on success, false on error (errors must be logged).</returns>
        protected virtual bool CheckConfiguration(IActivityMonitor monitor, TConfiguration safe)
        {
            return true;
        }

        Task<bool> IDeviceHost.TryStartAsync(IDevice d, IActivityMonitor monitor) => TryStartStop(d, monitor, true);
        
        Task<bool> IDeviceHost.TryStopAsync(IDevice d, IActivityMonitor monitor) => TryStartStop(d, monitor, false);


        async Task<bool> TryStartStop(IDevice d, IActivityMonitor monitor, bool start )
        {
            bool success = true;
            await _lock.WaitAsync();
            try
            {
                if (_devices.TryGetValue(d.Name, out var device))
                {
                    if( start )
                    {
                        ///....
                    }
                    else
                    {
                        ///...
                    }

                }
                else
                {
                    monitor.Error($"Attempting to Start/Stop a detached device ({d.Name}).");
                    success = false;
                }
            }
            catch (Exception ex)
            {
                monitor.Error(ex);
                success = false;
            }
            finally
            {
                _lock.Release();
            }
            return success;
        }
    }

    }
}
