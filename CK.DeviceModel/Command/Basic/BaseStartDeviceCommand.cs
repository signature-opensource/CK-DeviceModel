using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="StartDeviceCommand{THost}"/> command that
    /// attempts to start a device.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="StartDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class BaseStartDeviceCommand : DeviceCommandWithResult<bool>, ICompletable<bool>
    {
        private protected BaseStartDeviceCommand()
        {
        }

        void ICompletable<bool>.OnError( Exception ex, ref CompletionSource<bool>.OnError result ) => result.SetResult( false );
        void ICompletable<bool>.OnCanceled( ref CompletionSource<bool>.OnCanceled result) => result.SetResult( false );

        /// <summary>
        /// Obviously returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    }

}
