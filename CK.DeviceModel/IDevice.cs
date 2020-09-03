using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines non-generic device properties and methods.
    /// </summary>
    interface IDevice
    {
        /// <summary>
        /// Gets the name. Necessarily not null or whitespace.
        /// This name identifies the device in its host.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the full name of this device: it is "<see cref="IDeviceHost.DeviceHostName"/>/<see cref="Name"/>".
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets whether this device has been started.
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the current configuration status of this device.
        /// Just like <see cref="IsRunning"/>, since a device lives in multi-threaded/concurrent contexts,
        /// any sensible decision based on this "instant" status should be avoided.
        /// </summary>
        DeviceConfigurationStatus ConfigurationStatus { get; }

        /// <summary>
        /// Attempts to stop this device if it is running.
        /// The only reason a device cannot be stopped (and this method to return false) is because <see cref="ConfigurationStatus"/>
        /// is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if the device has been stopped, false if it is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.</returns>
        Task<bool> StopAsync( IActivityMonitor monitor );

        /// <summary>
        /// Attempts to start this device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if this device cannot start.</returns>
        Task<bool> StartAsync( IActivityMonitor monitor );

    }

}
