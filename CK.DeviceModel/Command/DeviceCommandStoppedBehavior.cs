using CK.Core;
using System;
using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the action that must be taken for commands that are handled when the device is stopped.
    /// The default behavior of a command is provided by the <see cref="BaseDeviceCommand.StoppedBehavior"/> protected property, 
    /// but this default behavior may be altered for any command by overriding the <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, BaseDeviceCommand)"/>
    /// protected method if the command's behavior is not <see cref="RunAnyway"/>, <see cref="SilentAutoStartAndStop"/> or <see cref="AutoStartAndKeepRunning"/>.
    /// <para>
    /// The default <see cref="BaseDeviceCommand.StoppedBehavior"/> is <see cref="WaitForNextStartWhenAlwaysRunningOrCancel"/>.
    /// </para>
    /// </summary>
    public enum DeviceCommandStoppedBehavior
    {
        /// <summary>
        /// The command is stored in an internal queue if the <see cref="IDevice.ConfigurationStatus"/> is <see cref="DeviceConfigurationStatus.AlwaysRunning"/>
        /// and as soon as the device is started, the command is executed, or the command is canceled.
        /// <para>
        /// This is the default.
        /// </para>
        /// </summary>
        WaitForNextStartWhenAlwaysRunningOrCancel,

        /// <summary>
        /// Same as <see cref="WaitForNextStartWhenAlwaysRunningOrCancel"/> except that an <see cref="UnavailableDeviceException"/> is set
        /// instead of canceling the command.
        /// </summary>
        WaitForNextStartWhenAlwaysRunningOrSetUnavailableDeviceException,

        /// <summary>
        /// The command will be executed regardless of the stopped status.
        /// </summary>
        RunAnyway,

        /// <summary>
        /// A <see cref="UnavailableDeviceException"/> is set on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        SetUnavailableDeviceException,

        /// <summary>
        /// <see cref="ICommandCompletionSource.SetCanceled()"/> is called on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        Cancel,

        /// <summary>
        /// The command is always stored in an internal queue when the device is stopped, waiting for its next start.
        /// This may be dangerous if the device is often stopped: the queue may grow too big.
        /// </summary>
        AlwaysWaitForNextStart,

        /// <summary>
        /// Automatically starts the device, executes the command and let it run (the deferred commands, if any, will then be executed).
        /// If the device cannot be started (without exception), the command will be canceled.
        /// </summary>
        AutoStartAndKeepRunning,

        /// <summary>
        /// Automatically starts the device, executes the command and stops the device without emitting device's status changes.
        /// If the device cannot be started (without exception), the command will be canceled.
        /// </summary>
        SilentAutoStartAndStop
    }

}
