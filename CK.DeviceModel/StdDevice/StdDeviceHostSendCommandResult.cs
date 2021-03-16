using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the outcome of the <see cref="StdDeviceHost.SendCommand(IActivityMonitor, StdDeviceCommand, CancellationToken)"/> method.
    /// This extends the <see cref="DeviceHostCommandResult"/> but replaces the <see cref="DeviceHostCommandResult.UnhandledCommandError"/> (that doesn't make sense)
    /// with the <see cref="SendCommandError"/> value.
    /// </summary>
    public enum StdDeviceHostSendCommandResult
    {
        /// <summary>
        /// The command has been successfully executed.
        /// </summary>
        Success = DeviceHostCommandResult.Success,

        /// <summary>
        /// The <see cref="DeviceCommand.HostType"/> is not the target one.
        /// </summary>
        InvalidHostType = DeviceHostCommandResult.InvalidHostType,

        /// <summary>
        /// The <see cref="DeviceCommand.CheckValidity(IActivityMonitor)"/> failed.
        /// </summary>
        CommandCheckValidityFailed = DeviceHostCommandResult.CommandCheckValidityFailed,

        /// <summary>
        /// The <see cref="DeviceCommand.DeviceName"/> target doesn't exist.
        /// </summary>
        DeviceNameNotFound = DeviceHostCommandResult.DeviceNameNotFound,

        /// <summary>
        /// The <see cref="DeviceCommand.ControllerKey"/> is not the expected one.
        /// </summary>
        ControllerKeyMismatch = DeviceHostCommandResult.ControllerKeyMismatch,

        /// <summary>
        /// The <see cref="StdDevice{TConfiguration}.SendCommand(IActivityMonitor, DeviceCommand, System.Threading.CancellationToken)"/>
        /// method returned false (the device has been destroyed).
        /// </summary>
        SendCommandError
    }

}
