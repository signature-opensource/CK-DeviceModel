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
        /// based on this "instant" status should be avoided (except if it is true: it never transitions
        /// from true to false).
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
        /// </summary>
        PerfectEvent<IDevice> StatusChanged { get; }

        /// <summary>
        /// Gets the current configuration status of this device.
        /// Just like <see cref="IsRunning"/>, since a device lives in multi-threaded/concurrent contexts,
        /// any sensible decision based on this "instant" status should be avoided.
        /// </summary>
        DeviceConfigurationStatus ConfigurationStatus { get; }

        /// <summary>
        /// Attempts to start this device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if this device cannot start.</returns>
        Task<bool> StartAsync( IActivityMonitor monitor );

        /// <summary>
        /// Attempts to stop this device if it is running.
        /// The only reason a device cannot be stopped (and this method to return false) is because <see cref="ConfigurationStatus"/>
        /// is <see cref="DeviceConfigurationStatus.AlwaysRunning"/> and <paramref name="ignoreAlwaysRunning"/> is false.
        /// </summary>
        /// <remarks>
        /// When the device is forced to stop (<paramref name="ignoreAlwaysRunning"/> is true), note that the <see cref="ConfigurationStatus"/> is left
        /// unchanged: the state of the system is what it should be: a device that has been configured to be always running is actually stopped.
        /// It is up to the <see cref="DeviceHostDaemon"/> to apply the <see cref="IDeviceAlwaysRunningPolicy"/> so that the device can be
        /// started again.
        /// </remarks>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ignoreAlwaysRunning">True to stop even if <see cref="ConfigurationStatus"/> states that this device must always run.</param>
        /// <returns>Always true except if <paramref name="ignoreAlwaysRunning"/> is false and the configuration is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.</returns>
        Task<bool> StopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false );

        /// <summary>
        /// Destroys this device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        Task DestroyAsync( IActivityMonitor monitor );

        /// <summary>
        /// Gets the current controller key. It can be null but not the empty string.
        /// When null, the <see cref="BaseDeviceCommand.ControllerKey"/> can be anything, but when this is not null, <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>
        /// checks that the command's controller key is the same as this one otherwise the command is not handled.
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
        /// </summary>
        PerfectEvent<IDevice, string?> ControllerKeyChanged { get; }

        /// <summary>
        /// Sends a command directly to this device instead of having it routed by <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>.
        /// By default, an <see cref="ArgumentException"/> is raised if:
        /// <list type="bullet">
        ///     <item><see cref="BaseDeviceCommand.HostType"/> is not compatible with this device's host type;</item>
        ///     <item>or <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> fails;</item>
        ///     <item>or the <see cref="BaseDeviceCommand.DeviceName"/> doesn't match this device's name;</item>
        /// </list>
        /// The last check can be suppressed thanks to the <paramref name="checkDeviceName"/>.
        /// <para>
        /// The <see cref="ControllerKey"/> check is done at the time when the command is executed: if the check fails, an <see cref="InvalidControllerKeyException"/>
        /// will be set on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>. The <paramref name="checkControllerKey"/> parameter
        /// can be used to skip this check.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="checkDeviceName">
        /// By default, the <see cref="BaseDeviceCommand.DeviceName"/> must be this <see cref="Name"/> otherwise an <see cref="ArgumentException"/> is thrown.
        /// Using false here allows any command name to be executed.
        /// </param>
        /// <param name="checkControllerKey">
        /// By default, the <see cref="BaseDeviceCommand.ControllerKey"/> must match this <see cref="ControllerKey"/> (when not null).
        /// Using false here skips this check.
        /// </param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool SendCommand( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default );

        /// <summary>
        /// Same as <see cref="SendCommand"/> except that only the host type
        /// is checked and <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> is called.
        /// <see cref="Name"/> or <see cref="ControllerKey"/> are ignored.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool UnsafeSendCommand( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default );

        /// <summary>
        /// Same as <see cref="SendCommand(IActivityMonitor, BaseDeviceCommand, bool, bool, CancellationToken)"/> except that the command
        /// is executed immediately.
        /// <para>
        /// Note that any <see cref="BaseDeviceCommand.StoppedBehavior"/> that may wait for the next start is ignored in this mode (deferring an
        /// "immediate" command doesn't make a lot of sense). This applies to <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/>
        /// (the command is canceled), <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrSetDeviceStoppedException"/> and
        /// <see cref="DeviceCommandStoppedBehavior.AlwaysWaitForNextStart"/> (a <see cref="UnavailableDeviceException"/> is set).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="checkDeviceName">
        /// By default, the <see cref="BaseDeviceCommand.DeviceName"/> must be this <see cref="Name"/> otherwise an <see cref="ArgumentException"/> is thrown.
        /// Using false here allows any command name to be executed.
        /// </param>
        /// <param name="checkControllerKey">
        /// By default, the <see cref="BaseDeviceCommand.ControllerKey"/> must match this <see cref="ControllerKey"/> (when not null).
        /// Using false here skips this check.
        /// </param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool SendCommandImmediate( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default );

        /// <summary>
        /// Same as <see cref="UnsafeSendCommand(IActivityMonitor, BaseDeviceCommand, CancellationToken)"/> except that the command
        /// is executed immediately.
        /// <para>
        /// Note that any <see cref="BaseDeviceCommand.StoppedBehavior"/> that may wait for the next start is ignored in this mode (deferring an
        /// "immediate" command doesn't make a lot of sense). This applies to <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/>
        /// (the command is canceled), <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrSetDeviceStoppedException"/> and
        /// <see cref="DeviceCommandStoppedBehavior.AlwaysWaitForNextStart"/> (a <see cref="UnavailableDeviceException"/> is set).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool UnsafeSendCommandImmediate( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default );
    }

}
