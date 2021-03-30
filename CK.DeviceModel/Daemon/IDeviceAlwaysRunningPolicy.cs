using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    /// <summary>
    /// Service that drives the policy of <see cref="DeviceConfigurationStatus.AlwaysRunning"/> devices that... don't run.
    /// The default <see cref="DefaultDeviceAlwaysRunningPolicy"/> is tailored to handle short-time disconnection: it
    /// calls <see cref="IDevice.StartAsync(IActivityMonitor)"/> only 4 times (with the following durations: 250, 500
    /// 500 and eventually 750 milliseconds) before giving up.
    /// <para>
    /// Calls to this policy are handled by the <see cref="DeviceHostDaemon"/> but manual (direct) calls to start can be done independently.
    /// </para>
    /// </summary>
    public interface IDeviceAlwaysRunningPolicy : ISingletonAutoService
    {
        /// <summary>
        /// Called each time a device that is supposed to be <see cref="DeviceConfigurationStatus.AlwaysRunning"/>
        /// has stopped: it can call <see cref="IDevice.StartAsync(IActivityMonitor)"/> (and, if start succeeded, should return 0).
        /// To stop trying to start the faulty device, simply returns a 0 or negative value.
        ///<para>
        /// Once the policy has given up, an independent call to the device's StartAsync that fails (or the
        /// next <see cref="IDevice.StopAsync(IActivityMonitor, bool)"/> after a successful restart),
        /// this policy will run again.
        ///</para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="host">The device host.</param>
        /// <param name="device">The faulty device.</param>
        /// <param name="retryCount">
        /// The number of previous attempts to restart the device (since the last time the device has stopped).
        /// For the very first attempt, this is 0. 
        /// </param>
        /// <returns>The number of millisecond to wait before the next retry or 0 to stop retrying.</returns>
        Task<int> RetryStartAsync( IActivityMonitor monitor, IDeviceHost host, IDevice device, int retryCount );
    }

}
