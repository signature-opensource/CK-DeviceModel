using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines whether devices mus be destroyed when <see cref="DeviceHostDaemon"/> stops.
    /// </summary>
    public enum OnStoppedDaemonBehavior
    {
        /// <summary>
        /// Nothing is done. Devices will die with the process.
        /// </summary>
        None,

        /// <summary>
        /// Asks all <see cref="IDeviceHost"/> to destroy their devices.
        /// </summary>
        ClearAllHosts,

        /// <summary>
        /// Asks all <see cref="IDeviceHost"/> to destroy their devices and wait for
        /// them to be destroyed.
        /// </summary>
        ClearAllHostsAndWaitForDevicesDestroyed,
    }
}
