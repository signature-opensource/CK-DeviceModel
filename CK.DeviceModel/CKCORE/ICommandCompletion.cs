using System;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Read only aspect of the <see cref="ICommandCompletionSource"/> that unifies <see cref="CommandCompletionSource"/>
    /// and <see cref="CommandCompletionSource{TResult}"/>
    /// </summary>
    public interface ICommandCompletion
    {
        /// <summary>
        /// Gets the task that will be resolved when the command completes.
        /// </summary>
        Task Task { get; }

        /// <summary>
        /// Gets whether the command completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets whether the command succeeded (SetResult or TrySetResult methods have been called).
        /// </summary>
        bool HasSucceed { get; }

        /// <summary>
        /// Gets whether the command failed (SetException or TrySetException have been called).
        /// </summary>
        bool HasFailed { get; }

        /// <summary>
        /// Gets whether the command has been canceled (SetCanceled or TrySetCanceled have been called).
        /// </summary>
        bool HasBeenCanceled { get; }
    }
}
