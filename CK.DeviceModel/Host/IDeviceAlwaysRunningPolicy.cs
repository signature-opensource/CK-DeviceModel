using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    /// <summary>
    /// Service that drives the policy of <see cref="DeviceConfigurationStatus.AlwaysRunning"/> devices that... don't run.
    /// The default <see cref="DefaultDeviceAlwaysRunningPolicy"/> is tailored to handle short-time disconnection: it
    /// calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> only 3 times (with the following durations: 500, 1000
    /// and 1000 milliseconds) before giving up.
    /// <para>
    /// Calls to this policy is handled by the <see cref="DeviceHostDaemon"/>.
    /// </para>
    /// </summary>
    public interface IDeviceAlwaysRunningPolicy : ISingletonAutoService
    {
        /// <summary>
        /// Called each time a device that is supposed to be <see cref="DeviceConfigurationStatus.AlwaysRunning"/>
        /// has stopped: it can call <see cref="IDevice.StartAsync(IActivityMonitor)"/>.
        /// To stop trying to start the faulty device, simply returns a 0 or negative value. Note that, in this case,
        /// an independant call to the device's StartAsync that fails (or the next stop after a successful restart), this
        /// policy will run again.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="host">The device host.</param>
        /// <param name="device">The faulty device.</param>
        /// <param name="retryCount">The number of previous attempts to restart the device (since the last time the device has stopped).</param>
        /// <returns>The number of millisecond to wait before the next retry or 0 to stop retrying.</returns>
        Task<int> RetryStartAsync( IActivityMonitor monitor, IDeviceHost host, IDevice device, int retryCount );
    }

}
