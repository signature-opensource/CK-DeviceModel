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
    /// This class cannot be directly specialized: the generic <see cref="HostedDeviceCommand{THost}"/>
    /// must be used.
    /// </summary>
    public abstract class DeviceCommand : DeviceCommandBase
    {
        private protected DeviceCommand()
        {
            Completion = new CommandCompletionSource();
        }

        private protected DeviceCommand( bool ignoreException, bool ignoreCanceled )
        {
            Completion = new CommandCompletionSource( ignoreException, ignoreCanceled );
        }

        /// <summary>
        /// Gets the <see cref="CommandCompletionSource"/> for this command.
        /// </summary>
        public CommandCompletionSource Completion { get; }

        internal override ICommandCompletionSource InternalCompletion => Completion;

    }
}
