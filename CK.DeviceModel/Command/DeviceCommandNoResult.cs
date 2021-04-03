using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base command that doesn't return a result: its <see cref="Completion"/> can be awaited either
    /// for completion or for error.
    /// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost}"/>
    /// must be used.
    /// </summary>
    public abstract class DeviceCommandNoResult : BaseDeviceCommand, IAsyncCommand
    {
        private protected DeviceCommandNoResult()
        {
            Completion = new CommandCompletionSource( this );
        }

        /// <summary>
        /// Gets the <see cref="CommandCompletionSource"/> for this command.
        /// </summary>
        public CommandCompletionSource Completion { get; }

        internal override ICommandCompletionSource InternalCompletion => Completion;

        /// <summary>
        /// Overridden to return this type name and <see cref="Completion"/> status.
        /// </summary>
        /// <returns>This type name and current completion status.</returns>
        public override string ToString() => GetType().Name + ' ' + Completion.ToString();
    }
}
