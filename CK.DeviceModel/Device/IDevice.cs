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
    /// <para>
    /// Only the immutable <see cref="Name"/>, <see cref="FullName"/> and the dynamic <see cref="ImmediateCommandLimitOffset"/>
    /// should be accessed through this interface.
    /// The other exposed properties should be accessed on <see cref="DeviceLifetimeEvent"/> properties:
    /// the event capture the relevant information at the time of the event whereas those device's properties
    /// can change at any moment.
    /// </para>
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
        /// <para>
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided.
        /// </para>
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets whether this device has been destroyed.
        /// <para>
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided (except if it is true: it never transitions
        /// from true to false).
        /// </para>
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Gets the <see cref="DeviceStatus"/> that captures the last change that occurred.
        /// <para>
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this "instant" status should be avoided.
        /// </para>
        /// </summary>
        public DeviceStatus Status { get; }

        /// <summary>
        /// Raised whenever a change occurred in the device status, controller key and/or configuration.
        /// </summary>
        PerfectEvent<DeviceLifetimeEvent> LifetimeEvent { get; }

        /// <summary>
        /// Gets a clone of the actual current configuration.
        /// This is NOT the actual configuration object reference that the device has received and
        /// is using: configuration objects are cloned in order to isolate the running device of any change
        /// in this publicly exposed configuration.
        /// <para>
        /// Since a device lives in multi-threaded/concurrent contexts, any sensible decision
        /// based on this property should be avoided.
        /// </para>
        /// <para>
        /// Even if changing this object is harmless, it should obviously not be changed.
        /// </para>
        /// </summary>
        DeviceConfiguration ExternalConfiguration { get; }

        /// <summary>
        /// Gets or sets an offset to the <see cref="DeviceConfiguration.BaseImmediateCommandLimit"/>.
        /// This can be changed at anytime by any thread, the actual limit (the sum of the two) will be
        /// between 1 and 1000.
        /// </summary>
        int ImmediateCommandLimitOffset { get; set; }

        /// <summary>
        /// Attempts to start this device. This sends a <see cref="BaseStartDeviceCommand"/> and await its completion.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if this device cannot start.</returns>
        Task<bool> StartAsync( IActivityMonitor monitor );

        /// <summary>
        /// Attempts to stop this device if it is running. This sends a <see cref="BaseStopDeviceCommand"/> and await its completion.
        /// The only reason a device cannot be stopped (and this method to return false) is because its <see cref="DeviceConfiguration.Status"/>
        /// is <see cref="DeviceConfigurationStatus.AlwaysRunning"/> and <paramref name="ignoreAlwaysRunning"/> is false.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ignoreAlwaysRunning">True to stop even if <see cref="DeviceConfiguration.Status"/> states that this device must always run.</param>
        /// <returns>Always true except if <paramref name="ignoreAlwaysRunning"/> is false and the configuration is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.</returns>
        Task<bool> StopAsync( IActivityMonitor monitor, bool ignoreAlwaysRunning = false );

        /// <summary>
        /// Reconfigures the device. This sends a <see cref="BaseConfigureDeviceCommand"/> and await its completion.
        /// Configuration's type must match the actual configuration type otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to apply.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>the result of the device configuration.</returns>
        Task<DeviceApplyConfigurationResult> ReconfigureAsync( IActivityMonitor monitor, DeviceConfiguration configuration, CancellationToken token = default );

        /// <summary>
        /// Destroys this device by sending an immediate <see cref="BaseDestroyDeviceCommand"/> and either returns <see cref="Task.CompletedTask"/>
        /// or the command completion's task depending on <paramref name="waitForDeviceDestroyed"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="waitForDeviceDestroyed">False to send the command and not wait for its completion.</param>
        /// <returns>The awaitable.</returns>
        Task DestroyAsync( IActivityMonitor monitor, bool waitForDeviceDestroyed = true );

        /// <summary>
        /// Gets the current controller key. It can be null but not the empty string.
        /// When null, the <see cref="BaseDeviceCommand.ControllerKey"/> can be anything, but when this is not null, <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>
        /// checks that the command's controller key is the same as this one otherwise the command is rejected.
        /// </summary>
        string? ControllerKey { get; }

        /// <summary>
        /// Sets a new <see cref="ControllerKey"/>, whatever its current value is (as long as it has not been fixed by
        /// configuration (the <see cref="DeviceConfiguration.ControllerKey"/>)).
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
        /// Tries to send a command directly to this device instead of having it routed by <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>.
        /// By default, an <see cref="ArgumentException"/> is raised if:
        /// <list type="bullet">
        ///     <item><see cref="BaseDeviceCommand.HostType"/> is not compatible with this device's host type;</item>
        ///     <item>or <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> fails;</item>
        ///     <item>or the <see cref="BaseDeviceCommand.DeviceName"/> doesn't match this device's name;</item>
        /// </list>
        /// The last check can be suppressed thanks to the <paramref name="checkDeviceName"/>.
        /// <para>
        /// Note that when this method returns false, the command completion has been called with an <see cref="UnavailableDeviceException"/> (and recall that
        /// depending on the command that may be transformed into a canceled or successful command task's result).
        /// </para>
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
        /// <param name="token">Optional cancellation token (added to <see cref="BaseDeviceCommand.AddCancellationSource(CancellationToken, string)"/>).</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool SendCommand( IActivityMonitor monitor,
                          BaseDeviceCommand command,
                          bool checkDeviceName = true,
                          bool checkControllerKey = true,
                          CancellationToken token = default );

        /// <summary>
        /// Same as <see cref="SendCommand"/> except that only the host type
        /// is checked and <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> is called.
        /// <see cref="Name"/> or <see cref="ControllerKey"/> are ignored.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional cancellation token (added to <see cref="BaseDeviceCommand.AddCancellationSource(CancellationToken,string)"/>).</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        bool UnsafeSendCommand( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default );

        /// <summary>
        /// Ensures that any existing commands have been fully handled: when the returned task is completed,
        /// you are assured that any previous commands have been fully handled.
        /// <para>
        /// For passive device, an internal command is sent and the returned task is its completion.
        /// </para>
        /// <para>
        /// It is more evolved for active device: an internal command that raises an event is sent and the returned task completes
        /// when the event should have been raised. This guaranties that all events that should have been emitted before calling this
        /// method have actually been emitted.
        /// </para>
        /// <para>
        /// This doesn't await long running commands (commands that are not completed directly by their handling but in the background).
        /// </para>
        /// </summary>
        /// <param name="considerDeferredCommands">True to consider all previous commands regardless of
        /// their <see cref="BaseDeviceCommand.StoppedBehavior"/> (any deferred commands waiting for the device to be running will be awaited).
        /// <para>
        /// When set to false, current deferred commands are ignored, the device can be stopped and only commands that are <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>,
        /// <see cref="DeviceCommandStoppedBehavior.SilentAutoStartAndStop"/> or <see cref="DeviceCommandStoppedBehavior.AutoStartAndKeepRunning"/> are considered.
        /// (In the latter case, there may be some deferred commands that have been handled before the returned task completes.)
        /// </para>
        /// </param>
        /// <param name="timeout">Optional timeout in milliseconds.</param>
        /// <param name="cancel">Optional cancellation token.</param>
        /// <returns>Success or whether the device is destroyed or <paramref name="cancel"/> has been signaled.</returns>
        Task<WaitForSynchronizationResult> WaitForSynchronizationAsync( bool considerDeferredCommands, int timeout = 0, CancellationToken cancel = default );

        /// <summary>
        /// Cancels all the commands that are waiting to be handled, either because they have been queued
        /// and not handled yet or because they are waiting for their <see cref="BaseDeviceCommand.SendingTimeUtc"/>
        /// or the device to be running.
        /// </summary>
        /// <param name="cancelQueuedCommands">Cancels the current command queue.</param>
        /// <param name="cancelDelayedCommands">
        /// Cancels delayed commands waiting for their <see cref="BaseDeviceCommand.SendingTimeUtc"/> and any
        /// registered <see cref="Device{TConfiguration}.AddReminder(DateTime, object?, bool)"/>.
        /// </param>
        /// <param name="cancelDeferredCommands">
        /// Cancels deferred commands waiting for the device to be running.
        /// </param>
        /// <returns>The number of queued, delayed and deferred commands that have been canceled.</returns>
        Task<(int,int,int)> CancelAllPendingCommandsAsync( bool cancelQueuedCommands,
                                                           bool cancelDelayedCommands,
                                                           bool cancelDeferredCommands );

    }

}
