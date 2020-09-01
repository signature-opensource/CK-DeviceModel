namespace CK.DeviceModel
{
    public enum ApplyConfigurationResult
    {
        /// <summary>
        /// New configuration has been applied.
        /// </summary>
        Success,

        /// <summary>
        /// The current <see cref="Device{TConfiguration}.Name"/> differ from the <see cref="IDeviceConfiguration.Name"/>.
        /// </summary>
        BadName,

        /// <summary>
        /// The new configuration is invalid (details are logged into the monitor).
        /// </summary>
        InvalidConfiguration,

        /// <summary>
        /// Changing the configuration would require a Start/Stop but restarting is disallowed.
        /// </summary>
        RestartRequired,

        /// <summary>
        /// The configuration triggered a Start (<see cref="DeviceConfigurationStatus.AlwaysRunning"/> or <see cref="DeviceConfigurationStatus.RunnableStarted"/>)
        /// but the Start failed.
        /// </summary>
        StartByConfigurationFailed,
    }

}
