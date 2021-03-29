namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the reason why a device is stopped.
    /// </summary>
    public enum DeviceStoppedReason
    {
        /// <summary>
        /// Not applicable.
        /// </summary>
        None = 0,

        /// <summary>
        /// The device stopped because of a <see cref="DeviceConfigurationStatus.Disabled"/>.
        /// </summary>
        StoppedByDisabledConfiguration,

        /// <summary>
        /// The device stopped because of a call to <see cref="IDevice.StopAsync(Core.IActivityMonitor, bool)"/>.
        /// </summary>
        StoppedCall,

        /// <summary>
        /// The device stopped because of a call to <see cref="IDevice.StopAsync(Core.IActivityMonitor, bool)"/>, ignoring the <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// </summary>
        StoppedForceCall,

        /// <summary>
        /// The device stopped because of a call to <see cref="IDevice.StopAsync(Core.IActivityMonitor, bool)"/> while it is handling a command.
        /// </summary>
        AutoStoppedCall,

        /// <summary>
        /// The device stopped because of a call to <see cref="IDevice.StopAsync(Core.IActivityMonitor, bool)"/>, ignoring the <see cref="DeviceConfigurationStatus.AlwaysRunning"/>,
        /// while it is handling a command.
        /// </summary>
        AutoStoppedForceCall,

        /// <summary>
        /// The device has stopped because it is being destroyed.
        /// </summary>
        Destroyed,

        /// <summary>
        /// The device has stopped because of a call to <see cref="IDevice.DestroyAsync(Core.IActivityMonitor)"/>,
        /// while it is handling a command.
        /// </summary>
        AutoDestroyed
    }
}


