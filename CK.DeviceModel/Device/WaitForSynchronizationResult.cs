using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Possible <see cref="IDevice.WaitForSynchronizationAsync"/> result.
    /// </summary>
    public enum WaitForSynchronizationResult
    {
        /// <summary>
        /// Previously sent commands have been handled.
        /// </summary>
        Success,

        /// <summary>
        /// The cancellation token has been signaled.
        /// </summary>
        Canceled,

        /// <summary>
        /// The timeout provided expired.
        /// </summary>
        Timeout,

        /// <summary>
        /// The device is destroyed.
        /// </summary>
        DeviceDestroyed
    }

}
