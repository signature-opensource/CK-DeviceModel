using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base command that doesn't return a result: its <see cref="Result"/> can be awaited either
    /// for completion or for error.
    /// This class cannot be directly specialized: the generic <see cref="HostedDeviceCommand{THost}"/>
    /// must be used.
    /// </summary>
    public abstract class DeviceCommand : DeviceCommandBase
    {
        private protected DeviceCommand()
        {
            Result = new CommandCompletion();
        }

        /// <summary>
        /// Gets the <see cref="CommandCompletion"/> for this command.
        /// </summary>
        public CommandCompletion Result { get; }

        /// <inheritdoc/>
        public override ICommandCompletion GetCompletionResult() => Result;

    }
}
