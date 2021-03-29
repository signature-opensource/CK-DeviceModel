using CK.Core;
using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the outcome of the <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/> method.
    /// </summary>
    public enum DeviceHostCommandResult
    {
        /// <summary>
        /// The command has been successfully sent.
        /// </summary>
        Success,

        /// <summary>
        /// The <see cref="BaseDeviceCommand.HostType"/> is not the target one.
        /// </summary>
        InvalidHostType,

        /// <summary>
        /// The <see cref="BaseDeviceCommand.CheckValidity(IActivityMonitor)"/> failed.
        /// </summary>
        CommandCheckValidityFailed,

        /// <summary>
        /// The <see cref="BaseDeviceCommand.DeviceName"/> target doesn't exist.
        /// </summary>
        DeviceNameNotFound,

        /// <summary>
        /// The <see cref="Device{TConfiguration}.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, bool, CancellationToken)"/>
        /// method returned false (the device has been destroyed).
        /// </summary>
        DeviceDestroyed
    }

}
