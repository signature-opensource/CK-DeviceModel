using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;

namespace CK.DeviceModel
{


    public abstract class Device<TConfiguration> : IDevice where TConfiguration : IDeviceConfiguration
    {
        internal IDeviceHost? _host;
        int _applyConfigurationCount;

        /// <summary>
        /// Initializes a new device bound to a configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        protected Device( TConfiguration config )
        {
            Name = config.Name;
        }

        /// <summary>
        /// Gets the name. Necessarily not null or whitespace.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the current <see cref="DeviceConfigurationStatus"/>.
        /// </summary>
        public DeviceConfigurationStatus ConfigurationStatus { get; private set; }

        internal async Task<ApplyConfigurationResult> ApplyConfigurationAsync(IActivityMonitor monitor, TConfiguration config, bool? allowRestart)
        {
            if (config.Name != Name) return ApplyConfigurationResult.BadName;
            var r = await DoApplyConfigurationAsync(monitor, config, allowRestart);
            if( r == ApplyConfigurationResult.Success )
            {
                if( Interlocked.Increment( ref _applyConfigurationCount ) == 1 )
                {
                    if( config.ConfigurationStatus == DeviceConfigurationStatus.RunnableStarted || config.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning )
                    {
                        if (!await DoStartAsync(monitor)) r = ApplyConfigurationResult.StartByConfigurationFailed;
                    }
                }
                else
                {
                    if (config.ConfigurationStatus == DeviceConfigurationStatus.Disabled)
                    {
                        await DoStopAsync(monitor, true);
                    }
                    else if (config.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning)
                    {
                        if (!await StartAsync(monitor)) r = ApplyConfigurationResult.StartByConfigurationFailed;
                    }
                }
            }
            return r;
        }

        /// <summary>
        /// Applies a new configuration.
        /// This can be called when this device is started or not, but <paramref name="allowRestart"/> should be set to true to allow a
        /// Start/Stop of the device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="config">The configuration to apply.</param>
        /// <param name="allowRestart">
        /// Defines the behavior regarding potentially required Stop/Start:
        /// <list type="bullet">
        ///     <item>true - Always allow restart.</item>
        ///     <item>false - Disallow any restart.</item>
        ///     <item>null - Allow restart as long as it has no important side effects.</item>
        /// </list>
        /// The host's global <see cref="ConfiguredDeviceHost{T, TConfiguration}.ApplyConfigurationAsync(IActivityMonitor, IConfiguredDeviceHostConfiguration{TConfiguration}, bool)"/>
        /// uses null here: a global reconfiguration SHOULD try to minimize side effects.
        /// </param>
        /// <returns>The result</returns>
        protected abstract Task<ApplyConfigurationResult> DoApplyConfigurationAsync(IActivityMonitor monitor, TConfiguration config, bool? allowRestart);

        internal protected virtual void Destroy( IActivityMonitor monitor )
        {
        }

        internal async Task<bool> HostStartAsync(IActivityMonitor monitor)
        {
            Debug.Assert(_host != null);
            // We are in the context of a call from the host, therefore we already have a lock (Semaphore lock). We can then safely call 
            // the DoStart of the device.
            return await DoStartAsync(monitor);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="monitor"></param>
        /// <returns></returns>
        public async Task<bool> StartAsync( IActivityMonitor monitor )
        {
            var h = _host;
            if( h == null )
            {
                monitor.Error("Starting a detached device is not possible.");
                return false;
            }
            return await h.TryStartAsync(this, monitor);
        }



        /// <summary>
        /// Agent starting method.
        /// </summary>
        /// <returns>True if the agent has been successfully started, false otherwise.</returns>
        internal protected abstract Task<bool> DoStartAsync(IActivityMonitor monitor);

        internal async Task<bool> HostStopAsync(IActivityMonitor monitor)
        {
            Debug.Assert(_host != null);
            return await DoStartAsync( monitor );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="monitor"></param>
        /// <returns></returns>
        public async Task<bool> StopAsync( IActivityMonitor monitor )
        {
            var h = _host;
            if( h == null )
            {
                monitor.Error("Starting a detached device is not possible.");
                return false;
            }
            return await h.TryStopAsync(this, monitor);
        }


        /// <summary>
        /// Agent stopping method. Should be redefined in derived classes, that should stop the specific agent.
        /// </summary>
        /// <returns>True if the agent has successfully stopped, false otherwise.</returns>
        internal protected abstract Task DoStopAsync(IActivityMonitor monitor, bool fromConfiguration );
    }

}
