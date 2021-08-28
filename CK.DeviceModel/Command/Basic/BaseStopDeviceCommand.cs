using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="StopDeviceCommand{THost}"/> command that
    /// stops a device.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="StopDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class BaseStopDeviceCommand : DeviceCommandWithResult<bool>, ICompletable<bool>
    {
        private protected BaseStopDeviceCommand()
        {
            ImmediateSending = true;
        }

        void ICompletable<bool>.OnError( Exception ex, ref CompletionSource<bool>.OnError result ) => result.SetResult( false );
        void ICompletable<bool>.OnCanceled( ref CompletionSource<bool>.OnCanceled result ) => result.SetResult( false );

        /// <summary>
        /// Gets or sets whether the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> should be ignored.
        /// </summary>
        public bool IgnoreAlwaysRunning { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/> (will be a no-op) since it must obviously not be deferred until the next start.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/> (will be a no-op).
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

    }

}
