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
        /// (this sould be used as the configuration key name for instance).
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
        /// Applies a configuration. Configuration's ype must match the actual type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <param name="allowEmptyConfiguration">By default, an empty configuration is considered an error.</param>
        /// <returns>True on success, false on error.</returns>
        Task<bool> ApplyConfigurationAsync( IActivityMonitor monitor, IDeviceHostConfiguration configuration, bool allowEmptyConfiguration = false );

        /// <summary>
        /// Handles <see cref="AsyncDeviceCommand"/> objects that will be routed to the device named <see cref="AsyncDeviceCommand.DeviceName"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="commmand">The command to execute.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        Task<bool> HandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand commmand );

        /// <summary>
        /// Handles <see cref="SyncDeviceCommand"/> objects that will be routed to the device named <see cref="SyncDeviceCommand.DeviceName"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="commmand">The command to execute.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        bool HandleCommand( IActivityMonitor monitor, SyncDeviceCommand commmand );

    }
}
