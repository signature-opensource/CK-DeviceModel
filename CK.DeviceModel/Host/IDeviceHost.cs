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
        /// Creates a <see cref="BaseConfigureDeviceCommand"/> with an existing or new empty Configuration
        /// of the correct type and an empty <see cref="BaseDeviceCommand.DeviceName"/>.
        /// <para>
        /// If a configuration object is provided, its type must match the actual type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="configuration">The existing configuration or null to initialize a new empty configuration object.</param>
        /// <returns>The reconfigure command.</returns>
        BaseConfigureDeviceCommand CreateConfigureCommand( DeviceConfiguration? configuration = null );

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
        /// Configuration's type must match the actual type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <returns>the result of the device configuration.</returns>
        Task<DeviceApplyConfigurationResult> EnsureDeviceAsync( IActivityMonitor monitor, DeviceConfiguration configuration );

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The device or null if not found.</returns>
        IDevice? Find( string deviceName );

        /// <summary>
        /// Gets a <see cref="PerfectEvent{IDeviceHost}"/> that is raised whenever one or more devices appeared or disappeared.
        /// </summary>
        PerfectEvent<IDeviceHost> DevicesChanged { get; }

        /// <summary>
        /// Gets a snapshot of the current devices indexed by name.
        /// This read only dictionary can be freely used (there is no concurrency issues), <see cref="DevicesChanged"/>
        /// event can be used to monitor changes.
        /// </summary>
        /// <returns>A snapshot of the devices.</returns>
        IReadOnlyDictionary<string,IDevice> GetDevices();

        /// <summary>
        /// Sends the provided command to the device it targets.
        /// <para>
        /// Determines whether the <see cref="BaseDeviceCommand.HostType"/> is compatible with the actual type of this host,
        /// finds the target device based on <see cref="BaseDeviceCommand.DeviceName"/> and calls
        /// <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> before sending the command to the device.
        /// </para>
        /// <para>
        /// The <see cref="IDevice.ControllerKey"/> check is done at the time when the command is executed: if the check fails, an <see cref="InvalidControllerKeyException"/>
        /// will be set on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>. The <paramref name="checkControllerKey"/> parameter
        /// can be used to skip this check.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to validate, route and send.</param>
        /// <param name="checkControllerKey">True to check the controller key right before executing the command.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The <see cref="DeviceHostCommandResult"/>.</returns>
        public DeviceHostCommandResult SendCommand( IActivityMonitor monitor, BaseDeviceCommand command, bool checkControllerKey = true, CancellationToken token = default );
    }
}
