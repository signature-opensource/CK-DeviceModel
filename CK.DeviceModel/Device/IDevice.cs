using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        /// Gets whether this device has been destroyed.
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided.
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Gets the <see cref="DeviceStatus"/> that captures the last change that occurred.
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided.
        /// </summary>
        public DeviceStatus Status { get; }

        /// <summary>
        /// Raised whenever a reconfiguration, a start or a stop happens: either the <see cref="IDevice.ConfigurationStatus"/>
        /// or <see cref="IDevice.Status"/> has changed.
        /// Reentrancy is forbidden: while handling this event, calling <see cref="StopAsync"/>, <see cref="StartAsync"/> or <see cref="IDeviceHost.ApplyConfigurationAsync"/>
        /// will throw a <see cref="LockRecursionException"/>.
        /// </summary>
        PerfectEvent<IDevice> StatusChanged { get; }

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
        /// Gets the current controller key.
        /// </summary>
        string? ControllerKey { get; }

        /// <summary>
        /// Sets a new <see cref="ControllerKey"/>, whatever its current value is.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="key">The controller key.</param>
        /// <returns>
        /// True if it has been changed, false otherwise, typically because the key has been fixed
        /// by the <see cref="DeviceConfiguration.ControllerKey"/>.
        /// </returns>
        Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? key );

        /// <summary>
        /// Sets a new <see cref="ControllerKey"/> only if the current one is the same as the specified <paramref name="current"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="current">The current value to challenge.</param>
        /// <param name="key">The controller key to set.</param>
        /// <returns>
        /// True if it has been changed, false otherwise: either the current key doesn't match the <paramref name="current"/>
        /// or the key has been fixed by configuration (the <see cref="DeviceConfiguration.ControllerKey"/>).
        /// </returns>
        Task<bool> SetControllerKeyAsync( IActivityMonitor monitor, string? current, string? key );

        /// <summary>
        /// Raised whenever the <see cref="ControllerKey"/> changed, either because of a reconfiguration or
        /// because of a call to <see cref="SetControllerKeyAsync(IActivityMonitor, string?)"/> or <see cref="SetControllerKeyAsync(IActivityMonitor, string?, string?)"/>.
        /// <para>
        /// When handling such event it is possible to call methods on this device since the async lock has been released.
        /// </para>
        /// </summary>
        PerfectEvent<IDevice, string?> ControllerKeyChanged { get; }

        /// <summary>
        /// Handles <see cref="AsyncDeviceCommand"/> command objects.
        /// <see cref="AsyncDeviceCommand.DeviceName"/> must match this <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Device implementations are invited to expose command objects and to handle them here in addition (if wanted) to any number
        /// of functions/methods they want.
        /// Command pattern is simple to implement and eases the integration with other architectural parts such as the
        /// Observable domain.
        /// </para>
        /// <para>
        /// Please note that when this method returns false it just means that the command has not been handled by this device.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="commmand">The command to handle.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        Task<bool> HandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand commmand );

        /// <summary>
        /// Handles <see cref="SyncDeviceCommand"/> command objects.
        /// <see cref="SyncDeviceCommand.DeviceName"/> must match this <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Device implementations are invited to expose command objects and to handle them here in addition (if wanted) to any number
        /// of functions/methods they want.
        /// Command pattern is simple to implement and eases the integration with other architectural parts such as the
        /// Observable domain.
        /// </para>
        /// <para>
        /// Please note that when this method returns false it just means that the command has not been handled by this device.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="commmand">The command to handle.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        bool HandleCommand( IActivityMonitor monitor, SyncDeviceCommand commmand );

    }

}
