using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the different status.
    /// </summary>
    public enum DeviceEventKind
    {
        /// <summary>
        /// Not applicable.
        /// </summary>
        None,

        /// <summary>
        /// The device has been created.
        /// </summary>
        Created,

        /// <summary>
        /// The device has been destroyed.
        /// </summary>
        Destroyed,

        /// <summary>
        /// The device has been started because of a <see cref="DeviceConfigurationStatus.RunnableStarted"/> or <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// </summary>
        StartedByConfiguration,

        /// <summary>
        /// The device started because of a call to 
        /// </summary>
        StartedByCall,

        /// <summary>
        /// The device stopped because of a <see cref="DeviceConfigurationStatus.Disabled"/>.
        /// </summary>
        StoppedByDisabledConfiguration,

        /// <summary>
        /// The device stopped because of a call to <see cref="Device{TConfiguration}.StopAsync(Core.IActivityMonitor)"/>.
        /// </summary>
        StoppedCall,

        /// <summary>
        /// The device has stopped because a fatal error has been encountered.
        /// </summary>
        StoppedOnFatalError

    }
}
