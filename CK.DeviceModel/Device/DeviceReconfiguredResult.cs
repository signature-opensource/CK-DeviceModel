using CK.Core;

namespace CK.DeviceModel
{

    /// <summary>
    /// Defines a subset of <see cref="DeviceApplyConfigurationResult"/> valid for a device reconfiguration:
    /// see <see cref="Device{TConfiguration}.DoReconfigureAsync(IActivityMonitor, TConfiguration)"/>.
    /// </summary>
    public enum DeviceReconfiguredResult
    {
        /// <summary>
        /// No reconfiguration happened.
        /// </summary>
        None = DeviceApplyConfigurationResult.None,

        /// <summary>
        /// The reconfiguration is successful.
        /// </summary>
        UpdateSucceeded = DeviceApplyConfigurationResult.UpdateSucceeded,


        /// <summary>
        /// The reconfiguration failed.
        /// </summary>
        UpdateFailed = DeviceApplyConfigurationResult.UpdateFailed,

        /// <summary>
        /// The updated configuration cannot be applied while the device is running.
        /// </summary>
        UpdateFailedRestartRequired = DeviceApplyConfigurationResult.UpdateFailedRestartRequired
    }

}
