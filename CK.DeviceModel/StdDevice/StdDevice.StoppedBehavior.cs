using CK.Core;
using System.Threading;

namespace CK.DeviceModel
{
    public abstract partial class StdDevice<TConfiguration>
    {
        /// <summary>
        /// Defines the action that must be taken for commands that should be handled when the device is stopped.
        /// </summary>
        public enum StoppedBehavior
        {
            /// <summary>
            /// A <see cref="CKException"/> is set on the final task.
            /// <para>
            /// Note that when <see cref="SendCommand(IActivityMonitor, DeviceCommand, CancellationToken)"/> is used, the
            /// command is silently skipped.
            /// </para>
            /// </summary>
            SetNotRunningException,

            /// <summary>
            /// The final task is canceled.
            /// <para>
            /// Note that when <see cref="SendCommand(IActivityMonitor, DeviceCommand, CancellationToken)"/> is used, the
            /// command is silently skipped.
            /// </para>
            /// </summary>
            Cancelled,

            /// <summary>
            /// The command will be executed (<see cref="HandleCommandAsync(IActivityMonitor, DeviceCommand, CancellationToken)"/> or <see cref="HandleCommandAsync{TResult}(IActivityMonitor, DeviceCommand{TResult}, CancellationToken)"/>
            /// will be called.
            /// </summary>
            Handle,

            /// <summary>
            /// The command is stored in an internal queue and as soon as the device is started, the command
            /// is executed. This enables commands to be resilient to temporary (unexpected) stops of the device and should be typically
            /// used in conjunction with <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
            /// </summary>
            WaitForNextStart
        }


    }
}
