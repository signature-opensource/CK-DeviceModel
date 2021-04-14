using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Implements the default retry policy that handles short-time disconnection for all the <see cref="IDeviceHost"/>.
    /// </summary>
    /// <remarks>
    /// Just like any <see cref="IAutoService"/>, this is replaceable, can be "covered" by a similar service by appearing in the constructor's argument
    /// or, since it is not sealed and <see cref="RetryStartAsync"/> is virtual, can be specialized.
    /// </remarks>
    public class DefaultDeviceAlwaysRunningPolicy : IDeviceAlwaysRunningPolicy
    {
        readonly int[] _retryTimeouts;

        /// <summary>
        /// Initializes a new <see cref="DefaultDeviceAlwaysRunningPolicy"/> with a list of retry timeouts (it applies to all hosts).
        /// </summary>
        /// <param name="retryTimeoutsMilliseconds">Timeouts to apply as long as restarting the device fails.</param>
        protected DefaultDeviceAlwaysRunningPolicy( int[] retryTimeoutsMilliseconds )
        {
            _retryTimeouts = retryTimeoutsMilliseconds;
        }

        /// <summary>
        /// Initializes a new <see cref="DefaultDeviceAlwaysRunningPolicy"/>.
        /// Default timeouts are: 250, 300, 500 and eventually 750 milliseconds.
        /// After 5 unsuccessful calls to <see cref="IDevice.StartAsync(IActivityMonitor)"/>, it gives up.
        /// See <see cref="Create(int[])"/>.
        /// </summary>
        public DefaultDeviceAlwaysRunningPolicy()
             : this( new int[] { 250, 300, 500, 750 } )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DefaultDeviceAlwaysRunningPolicy"/> with a list of retry timeouts (it applies to all hosts).
        /// </summary>
        /// <param name="retryTimeoutsMilliseconds">Timeouts to apply as long as restarting the device fails.</param>
        public static DefaultDeviceAlwaysRunningPolicy Create( params int[] retryTimeoutsMilliseconds )
        {
            return new DefaultDeviceAlwaysRunningPolicy( retryTimeoutsMilliseconds );
        }

        /// <summary>
        /// Calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> and, if StartAsync returned false, returns the configured
        /// timeout based on <paramref name="retryCount"/> 
        /// This returns 0 if the device has been started or the <paramref name="retryCount"/> is greater than the number of configured timeouts.
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
            return retryCount < _retryTimeouts.Length ? _retryTimeouts[retryCount] : 0;
        }
    }
}
