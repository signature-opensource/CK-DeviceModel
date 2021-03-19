using System;
using System.Collections.Generic;
using System.Text;

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
            Result = new CommandCompletion<TResult>();
        }

        /// <summary>
        /// Gets the <see cref="CommandCompletion"/> for this command.
        /// </summary>
        public CommandCompletion<TResult> Result { get; }

        /// <inheritdoc/>
        public override ICommandCompletion GetCompletionResult() => Result;

    }
}
