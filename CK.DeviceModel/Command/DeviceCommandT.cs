using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base device commands that generates a result.
    /// This class cannot be directly specialized: the generic <see cref="HostedDeviceCommand{THost,TResult}"/> must be used.
    /// </summary>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    public abstract class DeviceCommand<TResult> : DeviceCommandBase
    {
        private protected DeviceCommand()
        {
            Completion = new CommandCompletionSource<TResult>();
        }

        private protected DeviceCommand( TResult errorOrCancelResult )
        {
            Completion = new CommandCompletionSource<TResult>( errorOrCancelResult );
        }

        private protected DeviceCommand( Func<Exception, TResult>? transformError, bool ignoreCanceled, TResult cancelResult )
        {
            Completion = new CommandCompletionSource<TResult>( transformError, ignoreCanceled, cancelResult );
        }

        /// <summary>
        /// Gets the <see cref="CommandCompletionSource"/> for this command.
        /// </summary>
        public CommandCompletionSource<TResult> Completion { get; }

        internal override ICommandCompletionSource InternalCompletion => Completion;
    }
}
