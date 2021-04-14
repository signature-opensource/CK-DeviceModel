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
    public abstract class BaseDestroyDeviceCommand : DeviceCommandNoResult, ICompletable
    {
        private protected BaseDestroyDeviceCommand()
        {
        }

        void ICompletable.OnError( Exception ex, ref CompletionSource.OnError result) => result.SetResult();

        void ICompletable.OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetResult();

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the device can obviously be destroyed while stopped.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    }

}
