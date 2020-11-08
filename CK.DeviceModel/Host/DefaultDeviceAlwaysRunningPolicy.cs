using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Implements the default retry policy: handle short-time disconnection: it calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> only
    /// 4 times (with the following durations: 250, 500, 500 and eventually 750 milliseconds) before giving up.
    /// </summary>
    /// <remarks>
    /// Just like any <see cref="IAutoService"/>, this is replaceable, can be "covered" by a similar service by appearing in the constructor's argument
    /// or, since it is not sealed and <see cref="RetryStartAsync"/> is virtual, can be specialized.
    /// </remarks>
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
            if( await device.StartAsync( monitor ).ConfigureAwait( false ) )
            {
                return 0;
            }
            switch( retryCount )
            {
                case 0: return 250; 
                case 1:
                case 2: return 500;
                case 3: return 750;
                default: return 0;
            }
        }
    }
}
