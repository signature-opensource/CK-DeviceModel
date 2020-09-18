using System.Collections.Generic;

namespace CK.DeviceModel
{
    /// <summary>
    /// Describes the result of a device initial configuration or reconfiguration.
    /// This is returned for each device in <see cref="DeviceHost{T, THostConfiguration, TConfiguration}.ConfigurationResult"/>.
    /// </summary>
    public enum DeviceApplyConfigurationResult
    {
        /// <summary>
        /// No configuration happened.
        /// </summary>
        None,

        /// <summary>
        /// A new configuration is invalid: a new device failed to be instantiated.
        /// </summary>
        CreateFailed,

        /// <summary>
        /// A new configuration has successfully created a new device.
        /// </summary>
        CreateSucceeded,

        /// <summary>
        /// A new configuration has successfully created a new device that has been started (see <see cref="DeviceConfigurationStatus.AlwaysRunning"/> or <see cref="DeviceConfigurationStatus.RunnableStarted"/>).
        /// </summary>
        CreateAndStartSucceeded,

        /// <summary>
        /// A new configuration has successfully created a new device but its start failed (see <see cref="DeviceConfigurationStatus.AlwaysRunning"/> or <see cref="DeviceConfigurationStatus.RunnableStarted"/>).
        /// </summary>
        CreateSucceededButStartFailed,

        /// <summary>
        /// Changing the device's configuration has failed.
        /// </summary>
        UpdateFailed,

        /// <summary>
        /// The device's configuration has been changed.
        /// </summary>
        UpdateSucceeded,

        /// <summary>
        /// Changing the configuration requires the device to be stopped first.
        /// </summary>
        UpdateFailedRestartRequired,

        /// <summary>
        /// The device's configuration has been changed but the configured status (<see cref="DeviceConfigurationStatus.AlwaysRunning"/> or <see cref="DeviceConfigurationStatus.RunnableStarted"/>)
        /// implied a Start that failed.
        /// </summary>
        UpdateSucceededButStartFailed,

        /// <summary>
        /// The host has considered that the whole configuration was invalid.
        /// </summary>
        InvalidHostConfiguration
    }

}
