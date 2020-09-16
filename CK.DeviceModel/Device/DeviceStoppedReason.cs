namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the reason why a device stopped.
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
        /// The device stopped because of a call to the public <see cref="IDevice.StopAsync(Core.IActivityMonitor)"/>.
        /// </summary>
        StoppedCall,

        /// <summary>
        /// The device stopped because of a call to the protected <see cref="Device{TConfiguration}.AutoStopAsync(Core.IActivityMonitor, bool)"/>.
        /// </summary>
        AutoStoppedCall,

        /// <summary>
        /// The device stopped because of a call to the protected <see cref="Device{TConfiguration}.AutoStopAsync(Core.IActivityMonitor, bool)"/>
        /// with ignoreAlwaysRunning parameter set to true.
        /// </summary>
        AutoStoppedForceCall,

        /// <summary>
        /// The device has stopped because it will be destroyed.
        /// </summary>
        StoppedBeforeDestroy
    }
}


