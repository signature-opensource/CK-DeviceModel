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
        /// Gets whether this <see cref="Task"/> will be in <see cref="TaskStatus.RanToCompletion"/> whenever <see cref="SetException(Exception)"/>
        /// or <see cref="TrySetException(Exception)"/> have been called.
        /// </summary>
        bool IgnoreException { get; }

        /// <summary>
        /// Gets whether this <see cref="Task"/> will be in <see cref="TaskStatus.RanToCompletion"/> whenever <see cref="SetCanceled()"/>
        /// or <see cref="TrySetCanceled()"/> have been called.
        /// </summary>
        bool IgnoreCanceled { get; }

        /// <summary>
        /// Gets whether the command completed.
        /// This is independent of <see cref="IgnoreException"/> or <see cref="IgnoreCanceled"/>.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets whether the command succeeded (SetResult or TrySetResult methods have been called).
        /// </summary>
        bool IsSuccessful { get; }

        /// <summary>
        /// Gets whether the command failed (<see cref="SetException(Exception)"/> or <see cref="TrySetException(Exception)"/> have been called).
        /// </summary>
        bool IsError { get; }

        /// <summary>
        /// Gets whether the command has been canceled (<see cref="SetCanceled()"/> or <see cref="TrySetCanceled()"/> have been called).
        /// </summary>
        bool IsCanceled { get; }
    }
}
