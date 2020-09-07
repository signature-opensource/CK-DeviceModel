using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines non-generic device properties and methods.
    /// </summary>
    public interface IDevice
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

        /// <summary>
        /// Raised whenever a reconfiguration, a start or a stop happens.
        /// This is the synchronous form of the event. If asynchronous calls must be made, <see cref="StateChangedAsync"/>
        /// or <see cref="StateChangedParallelAsync"/> must be used.
        /// </summary>
        event SequentialEventHandler<IDevice, DeviceStateChangedEvent> StateChanged;

        /// <summary>
        /// Raised whenever a reconfiguration, a start or a stop happens and enables asynchronous handling of this event.
        /// The multiple registered delegates are called one after the others. If parallel asynchronous actions are
        /// possible, <see cref="StateChangedParallelAsync"/> can be used.
        /// </summary>
        event SequentialEventHandlerAsync<IDevice, DeviceStateChangedEvent> StateChangedAsync;

        /// <summary>
        /// Raised whenever a reconfiguration, a start or a stop happens and enables asynchronous handling of this event.
        /// All registered delegates are called in parallel. 
        /// </summary>
        event ParallelEventHandlerAsync<IDevice, DeviceStateChangedEvent> StateChangedParallelAsync;

    }

}
