using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the outcome of the <see cref="DeviceHost.SendCommand(IActivityMonitor, DeviceCommandBase, CancellationToken)"/> method.
    /// </summary>
    public enum DeviceHostCommandResult
    {
        /// <summary>
        /// The command has been successfully sent.
        /// </summary>
        Success,

        /// <summary>
        /// The <see cref="DeviceCommand.HostType"/> is not the target one.
        /// </summary>
        InvalidHostType,

        /// <summary>
        /// The <see cref="DeviceCommandBase.GetCompletionResult"/> has already been completed.
        /// Commands cannot be reused.
        /// </summary>
        CommandAlreadyUsed,

        /// <summary>
        /// The <see cref="DeviceCommand.CheckValidity(IActivityMonitor)"/> failed.
        /// </summary>
        CommandCheckValidityFailed,

        /// <summary>
        /// The <see cref="DeviceCommand.DeviceName"/> target doesn't exist.
        /// </summary>
        DeviceNameNotFound,

        /// <summary>
        /// The <see cref="DeviceCommand.ControllerKey"/> is not the expected one.
        /// </summary>
        ControllerKeyMismatch,

        /// <summary>
        /// The <see cref="Device{TConfiguration}.SendCommand(IActivityMonitor, DeviceCommandBase, System.Threading.CancellationToken)"/>
        /// method returned false (the device has been destroyed).
        /// </summary>
        DeviceDestroyed
    }

}
