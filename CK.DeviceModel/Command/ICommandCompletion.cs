using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Unifies <see cref="CommandCompletion"/> and <see cref="CommandCompletion{TResult}"/>.
    /// Note that thanks to the <see cref="GetAwaiter()"/>, this is awaitable.
    /// </summary>
    public interface ICommandCompletion
    {
        /// <summary>
        /// Gets the error if an error occurred.
        /// This is null as long as <see cref="IsCompleted"/> is false or if <see cref="Success"/> is true.
        /// </summary>
        Exception? Error { get; }

        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and an <see cref="Error"/> occurred.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Error ) )]
        bool HasError { get; }

        /// <summary>
        /// Gets whether <see cref="Success"/> is no more null (Success is either true or false).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets whether command has not been completed (null when <see cref="IsCompleted"/> is false),
        /// whether an error occurred (false when <see cref="HasError"/> is true),
        /// or whether the command has been successfully executed.
        /// </summary>
        bool? Success { get; }

        /// <summary>
        /// Sets a cancellation error: the <see cref="Error"/> will be a <see cref="OperationCanceledException"/>.
        /// Can be called only once and only if SetSuccess nor <see cref="SetError(Exception)"/> have been called yet.
        /// </summary>
        void SetCanceled();

        /// <summary>
        /// Sets an error.
        /// Can be called only once and only if SetSuccess nor <see cref="SetCanceled()"/> have been called yet.
        /// </summary>
        /// <param name="error">The exception.</param>
        void SetError( Exception error );

        /// <summary>
        /// Asynchronously waits for this CommandCompletion to be resolved within a maximum amount of time (and/or as long
        /// as the <paramref name="cancellation"/> is not signaled). 
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout in milliseconds to wait before returning false.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>True if <see cref="IsCompleted"/> is true, false if the timeout occurred before.</returns>
        Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default );

        /// <summary>
        /// Enables this <see cref="ICommandCompletion"/> to be awaited.
        /// </summary>
        /// <returns>An awaiter for this command completion.</returns>
        ICriticalNotifyCompletion GetAwaiter();
    }
}
