using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the outcome of the <see cref="IDeviceHost.ExecuteCommandAsync(IActivityMonitor, DeviceCommand)"/> method.
    /// </summary>
    public enum DeviceHostCommandResult
    {
        /// <summary>
        /// The command has been successfully executed.
        /// </summary>
        Success,

        /// <summary>
        /// The <see cref="DeviceCommand.HostType"/> is not the target one.
        /// </summary>
        InvalidHostType,

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
        /// The command execution raised an exception that has been logged.
        /// </summary>
        UnhandledCommandError
    }

}
