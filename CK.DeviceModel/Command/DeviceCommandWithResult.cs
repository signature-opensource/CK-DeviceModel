using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base device commands that generates a result.
    /// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost,TResult}"/> must be used.
    /// </summary>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    public abstract class DeviceCommandWithResult<TResult> : BaseDeviceCommand, ICompletable<TResult>
    {
        private protected DeviceCommandWithResult()
        {
            Completion = new CompletionSource<TResult>( this );
        }

        /// <summary>
        /// Gets the <see cref="CompletionSource"/> for this command.
        /// </summary>
        public CompletionSource<TResult> Completion { get; }

        /// <summary>
        /// Gets the <see cref="CompletionSource"/> for this command.
        /// </summary>
        ICompletion<TResult> ICompletable<TResult>.Completion => Completion;

        internal override ICompletionSource InternalCompletion => Completion;

        /// <summary>
        /// Overridden to return this type name and <see cref="Completion"/> status.
        /// </summary>
        /// <returns>This type name and current completion status.</returns>
        public override string ToString() => GetType().Name + '<' + typeof( TResult ).Name + "> " + Completion.ToString();

    }
}
