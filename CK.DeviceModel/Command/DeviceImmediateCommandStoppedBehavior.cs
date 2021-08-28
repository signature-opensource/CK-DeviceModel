using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the action that must be taken for commands sent to be immediately handled when the device is stopped.
    /// The default behavior of a command is provided by the <see cref="BaseDeviceCommand.StoppedBehavior"/> protected property, 
    /// but this default behavior may be altered for any command by overriding the <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, BaseDeviceCommand)"/>
    /// protected method if the command's behavior is not <see cref="RunAnyway"/>, <see cref="SilentAutoStartAndStop"/> or <see cref="AutoStartAndKeepRunning"/>.
    /// <para>
    /// The default <see cref="BaseDeviceCommand.StoppedBehavior"/> is <see cref="WaitForNextStartWhenAlwaysRunningOrCancel"/>.
    /// </para>
    /// </summary>
    public enum DeviceImmediateCommandStoppedBehavior
    {
        /// <summary>
        /// The command will be executed regardless of the stopped status.
        /// </summary>
        RunAnyway,

        /// <summary>
        /// A <see cref="UnavailableDeviceException"/> is set on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        SetUnavailableDeviceException,

        /// <summary>
        /// <see cref="ICompletionSource.SetCanceled()"/> is called on the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        Cancel
    }

}
