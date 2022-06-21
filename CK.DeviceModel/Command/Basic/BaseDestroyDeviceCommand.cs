using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="DestroyDeviceCommand{THost}"/> command that
    /// destroys a device.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="DestroyDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class BaseDestroyDeviceCommand : DeviceCommandNoResult
    {
        private protected BaseDestroyDeviceCommand()
        {
            ImmediateSending = true;
            ShouldCallDeviceOnCommandCompleted = false;
        }

        /// <summary>
        /// Transforms any error into successful result.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <param name="result">The result setter.</param>
        protected override sealed void OnError( Exception ex, ref CompletionSource.OnError result ) => result.SetResult();

        /// <summary>
        /// Transforms cancellation into successful result.
        /// </summary>
        /// <param name="result">The result setter.</param>
        protected override sealed void OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetResult();

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the device can obviously be destroyed while stopped.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override sealed DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/>: the device can obviously be destroyed while stopped.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override sealed DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

    }

}
