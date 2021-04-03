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
    public abstract class BaseDestroyDeviceCommand : DeviceCommandNoResult, IAsyncCommand
    {
        private protected BaseDestroyDeviceCommand()
        {
        }

        void IAsyncCommand.OnError( Exception ex, ref CommandCompletionSource.OnError result) => result.SetResult();

        void IAsyncCommand.OnCanceled( ref CommandCompletionSource.OnCanceled result ) => result.SetResult();

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the device can obviously be destroyed while stopped.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    }

}
