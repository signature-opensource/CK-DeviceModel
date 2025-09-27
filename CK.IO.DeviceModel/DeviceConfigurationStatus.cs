namespace CK.IO.DeviceModel;

/// <summary>
/// Models the 4 states of a device's running status from its configuration.
/// </summary>
public enum DeviceConfigurationStatus
{
    /// <summary>
    /// The device cannot <see cref="Device{TConfiguration}.StartAsync(Core.IActivityMonitor)"/>.
    /// </summary>
    Disabled,

    /// <summary>
    /// The device can be started or stopped. Applying this configuration doesn't change its running status.
    /// </summary>
    Runnable,

    /// <summary>
    /// The device can be started or stopped (just like <see cref="Runnable"/>), but an attempt to start it will 
    /// be done right after its creation.
    /// </summary>
    RunnableStarted,

    /// <summary>
    /// The device should always be running.
    /// </summary>
    AlwaysRunning
}
