using System.Collections.Generic;

namespace CK.DeviceModel
{
    /// <summary>
    /// Describes the result of a device initial configuration or reconfiguration.
    /// </summary>
    public enum ReconfigurationResult
    {
        /// <summary>
        /// Not applicable.
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
        /// Changing the configuration would require a Start/Stop but restarting is disallowed.
        /// </summary>
        UpdateFailedRestartRequired,

        /// <summary>
        /// The device's configuration has been changed but the configured status (<see cref="DeviceConfigurationStatus.AlwaysRunning"/> or <see cref="DeviceConfigurationStatus.RunnableStarted"/>)
        /// implied a Start that failed.
        /// </summary>
        UpdateSucceededButStartFailed
    }

}
