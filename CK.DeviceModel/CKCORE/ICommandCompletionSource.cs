using System;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Unifies <see cref="CommandCompletionSource"/> and <see cref="CommandCompletionSource{TResult}"/>
    /// </summary>
    public interface ICommandCompletionSource : ICommandCompletion
    {
        /// <summary>
        /// Transitions the <see cref="ICommandCompletion.Task"/> into the <see cref="TaskStatus.Faulted"/> state (this
        /// can be changed by the command's overridden <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation).
        /// <para>
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states (<see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>)00.
        /// </para>
        /// <para>
        /// Note that, on success, <see cref="ICommandCompletion.HasFailed"/> is set to true, regardless of any alteration of
        /// the Task's result by Command's <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation.
        /// </para>
        /// </summary>
        void SetException( Exception exception );

        /// <summary>
        /// Attempts to transition the <see cref="ICommandCompletion.Task"/> into the <see cref="TaskStatus.Faulted"/> state (this
        /// can be changed by the command's overridden <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation).
        /// <para>
        /// Note that, on success, <see cref="ICommandCompletion.HasFailed"/> is set to true, regardless of any alteration of
        /// the Task's result by Command's <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation.
        /// </para>
        /// </summary> 
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        bool TrySetException( Exception exception );

        /// <summary>
        /// Transitions the <see cref="ICommandCompletion.Task"/> into the <see cref="TaskStatus.Canceled"/> state (this can
        /// be changed by the command's overridden <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation).
        /// <para>
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states (<see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>)00.
        /// </para>
        /// <para>
        /// Note that, on success, <see cref="ICommandCompletion.HasBeenCanceled"/> is set to true, regardless of any alteration of
        /// the Task's result by Command's <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation.
        /// </para>
        /// </summary>
        void SetCanceled();

        /// <summary>
        /// Attempts to transition the <see cref="ICommandCompletion.Task"/> into the <see cref="TaskStatus.Canceled"/> state (this can
        /// be changed by the command's overridden <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation).
        /// <para>
        /// Note that, on success, <see cref="ICommandCompletion.HasBeenCanceled"/> is set to true, regardless of any alteration of
        /// the Task's result by Command's <see cref="IAsyncCommand"/> or <see cref="IAsyncCommand{TResult}"/> implementation.
        /// </para>
        /// </summary>
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        bool TrySetCanceled();
    }
}
