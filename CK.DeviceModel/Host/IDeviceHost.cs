using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines non-generic device host properties.
    /// </summary>
    [IsMultiple]
    public interface IDeviceHost : ISingletonAutoService
    {
        /// <summary>
        /// Gets the host name that SHOULD identify this host instance unambiguously in a running context.
        /// (this should be used as the configuration key name for instance).
        /// </summary>
        string DeviceHostName { get; }

        /// <summary>
        /// Gets the number of devices.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Clears this host by stopping and destroying all existing devices.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        Task ClearAsync( IActivityMonitor monitor );

        /// <summary>
        /// Gets the type of the object that configures this host (the <see cref="DeviceHostConfiguration{TConfiguration}"/> that is used).
        /// </summary>
        /// <returns>The type of the host configurations.</returns>
        Type GetDeviceHostConfigurationType();

        /// <summary>
        /// Gets the type of the object that configures the device.
        /// </summary>
        /// <returns>The type of the device configuration.</returns>
        Type GetDeviceConfigurationType();

        /// <summary>
        /// Applies a configuration. Configuration's type must match the actual type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <param name="allowEmptyConfiguration">By default, an empty configuration is considered an error.</param>
        /// <returns>True on success, false on error.</returns>
        Task<bool> ApplyConfigurationAsync( IActivityMonitor monitor, IDeviceHostConfiguration configuration, bool allowEmptyConfiguration = false );

        /// <summary>
        /// Applies a device configuration: this ensures that the device exists (it is created if needed) and is configured by the provided <paramref name="configuration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <returns>the result of the device configuration.</returns>
        Task<DeviceApplyConfigurationResult> ApplyDeviceConfigurationAsync( IActivityMonitor monitor, DeviceConfiguration configuration );

        /// <summary>
        /// Attempts to destroy a device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="deviceName">The device name.</param>
        /// <returns>The awaitable.</returns>
        Task DestroyDeviceAsync( IActivityMonitor monitor, string deviceName );

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        IDevice? Find( string deviceName );

        /// <summary>
        /// Gets a device and its applied configuration by its name.
        /// See <see cref="ConfiguredDevice{T, TConfiguration}.Configuration"/>.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device and its configuration (or null references if not found).</returns>
        (IDevice?, DeviceConfiguration?) GetConfiguredDevice( string deviceName );

        /// <summary>
        /// Gets a snapshot of the current devices and their configurations that satisfy a predicate.
        /// Note that these objects are a copy of the ones that are used by the actual devices.
        /// See <see cref="ConfiguredDevice{T, TConfiguration}.Configuration"/>.
        /// </summary>
        /// <param name="predicate">Optional predicate to filter the snapshotted result.</param>
        /// <returns>The snapshot of the configured devices.</returns>
        IReadOnlyList<(IDevice, DeviceConfiguration)> GetConfiguredDevices( Func<IDevice, DeviceConfiguration, bool>? predicate = null );

        /// <summary>
        /// Gets a <see cref="PerfectEvent{IDeviceHost}"/> that is raised whenever the device list has changed
        /// or any device's configuration has changed.
        /// This event is not raised when devices are started or stopped or when their <see cref="IDevice.ControllerKey"/> changed.
        /// </summary>
        PerfectEvent<IDeviceHost> DevicesChanged { get; }

        /// <summary>
        /// Determines whether the <see cref="DeviceCommand.HostType"/> is compatible with the actual type of this host,
        /// finds the target device based on <see cref="DeviceCommand.DeviceName"/>, checks the <see cref="DeviceCommand.ControllerKey"/>
        /// against the <see cref="IDevice.ControllerKey"/> and calls <see cref="DeviceCommand.CheckValidity(IActivityMonitor)"/> before
        /// submitting the command to the device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to validate, route and execute.</param>
        /// <returns>The <see cref="DeviceHostCommandResult"/>.</returns>
        Task<DeviceHostCommandResult> ExecuteCommandAsync( IActivityMonitor monitor, DeviceCommand command );

    }
}
