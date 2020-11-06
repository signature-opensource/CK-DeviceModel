using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Implements the default retry policy: handle short-time disconnection: it calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> only
    /// 3 times (with the following durations: 500ms, 1s and 1s) before giving up.
    /// </summary>
    public class DefaultDeviceAlwaysRunningPolicy : IDeviceAlwaysRunningPolicy
    {
        /// <summary>
        /// Calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> and returns 500, 1000 milliseconds or 0 when
        /// the device has been started or the <paramref name="retryCount"/> is greater than 2.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="host">The device host.</param>
        /// <param name="device">The faulty device.</param>
        /// <param name="retryCount">The number of previous attempts to restart the device (since the last time the device has stopped).</param>
        /// <returns>The number of millisecond to wait before the next retry or 0 to stop retrying.</returns>
        public virtual async Task<int> RetryStartAsync( IActivityMonitor monitor, IDeviceHost host, IDevice device, int retryCount )
        {
            if( await device.StartAsync( monitor ) )
            {
                return 0;
            }
            switch( retryCount )
            {
                case 0: return 500; 
                case 1: 
                case 2: return 1000;
                default: return 0;
            }
        }
    }
}
