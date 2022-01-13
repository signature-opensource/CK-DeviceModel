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
    public abstract class BaseStopDeviceCommand : DeviceCommandWithResult<bool>
    {
        private protected BaseStopDeviceCommand()
        {
            ImmediateSending = true;
            ShouldCallDeviceOnCommandCompleted = false;
        }

        /// <summary>
        /// Transforms any error into false result.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <param name="result">The result setter.</param>
        protected override sealed void OnError( Exception ex, ref CompletionSource<bool>.OnError result )
        {
            result.SetResult( false );
        }

        /// <summary>
        /// Transforms cancellation into false result.
        /// </summary>
        /// <param name="result">The result setter.</param>
        protected override sealed void OnCanceled( ref CompletionSource<bool>.OnCanceled result )
        {
            result.SetResult( false );
        }

        /// <summary>
        /// Gets or sets whether the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> should be ignored.
        /// </summary>
        public bool IgnoreAlwaysRunning { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/> (will be a no-op) since it must obviously not be deferred until the next start.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override sealed DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/> (will be a no-op).
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override sealed DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

    }

}
