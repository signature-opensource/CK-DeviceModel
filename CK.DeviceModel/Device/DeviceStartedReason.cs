namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the reason why a device started.
    /// </summary>
    public enum DeviceStartedReason
    {
        /// <summary>
        /// Not applicable.
        /// </summary>
        None = 0,

        /// <summary>
        /// The device started because of a <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// </summary>
        StartedByAlwaysRunningConfiguration,

        /// <summary>
        /// The device started right after its creation because of a configuration of <see cref="DeviceConfigurationStatus.RunnableStarted"/>.
        /// </summary>
        StartedByRunnableStartedConfiguration,

        /// <summary>
        /// The device started because of a call to <see cref="IDevice.StartAsync(Core.IActivityMonitor)"/>.
        /// </summary>
        StartCall,

        /// <summary>
        /// The device started because of a call to <see cref="IDevice.StartAsync(Core.IActivityMonitor)"/> while it is handling a command.
        /// </summary>
        SelfStart,

        /// <summary>
        /// The device started because of a <see cref="DeviceCommandStoppedBehavior.AutoStartAndKeepRunning"/> command's <see cref="BaseDeviceCommand.StoppedBehavior"/>.
        /// </summary>
        StartAndKeepRunningStoppedBehavior,

        /// <summary>
        /// The device started because of a <see cref="DeviceCommandStoppedBehavior.SilentAutoStartAndStop"/> command's <see cref="BaseDeviceCommand.StoppedBehavior"/>.
        /// This status is not published to the external world.
        /// </summary>
        SilentAutoStartAndStopStoppedBehavior,
    }

}
