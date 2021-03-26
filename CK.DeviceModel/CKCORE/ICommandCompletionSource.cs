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
        /// Transitions the <see cref="Task"/> into the <see cref="TaskStatus.Faulted"/> state or into the <see cref="TaskStatus.RanToCompletion"/>
        /// if <see cref="IgnoreCanceled"/> is true.
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>.
        /// <para>
        /// On success, <see cref="ICommandCompletion.IsError"/> is set to true.
        /// </para>
        /// </summary>
        void SetException( Exception exception );

        /// <summary>
        /// Attempts to transition the underlying Task into the <see cref="TaskStatus.Faulted"/> state or into the <see cref="TaskStatus.RanToCompletion"/>
        /// if <see cref="ICommandCompletion.IgnoreException"/> is true.
        /// <para>
        /// On success, <see cref="ICommandCompletion.IsError"/> is set to true.
        /// </para>
        /// </summary> 
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        bool TrySetException( Exception exception );

        /// <summary>
        /// Transitions the underlying Task into the <see cref="TaskStatus.Canceled"/> state or into the <see cref="TaskStatus.RanToCompletion"/>
        /// if <see cref="ICommandCompletion.IgnoreCanceled"/> is true.
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>.
        /// <para>
        /// On success, <see cref="ICommandCompletion.IsCanceled"/> is set to true.
        /// </para>
        /// </summary>
        void SetCanceled();

        /// <summary>
        /// Attempts to transition the underlying Task into the <see cref="TaskStatus.Canceled"/> state or into the <see cref="TaskStatus.RanToCompletion"/>
        /// if <see cref="ICommandCompletion.IgnoreCanceled"/> is true.
        /// <para>
        /// On success, <see cref="ICommandCompletion.IsCanceled"/> is set to true.
        /// </para>
        /// </summary>
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        bool TrySetCanceled();
    }
}
