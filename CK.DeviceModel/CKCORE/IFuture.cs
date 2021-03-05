using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// A future is a little bit like a Task except that it is not directly awaitable: you can <see cref="WaitAsync(int, CancellationToken)"/> a future
    /// with a given timeout and a cancellation token, but cannot await or set a continuation directly on it directly (you may use <see cref="AsTask()"/> however).
    /// <para>
    /// This is to be used for long running processes that are typically fully asynchronous and/or externally implemented. 
    /// </para>
    /// </summary>
    public interface IFuture
    {
        /// <summary>
        /// Gets the error if an error occurred.
        /// This is null as long as <see cref="IsCompleted"/> is false or if <see cref="Success"/> is true.
        /// </summary>
        CKExceptionData? Error { get; }

        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and an <see cref="Error"/> occurred.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Error ) )]
        bool HasError { get; }

        /// <summary>
        /// Gets whether <see cref="Success"/> is no more null (it is either true or false).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets whether the result is not yet available (null when <see cref="IsCompleted"/> is false),
        /// whether an error occurred (false when <see cref="HasError"/> is true),
        /// or whether the command has been successfully executed.
        /// </summary>
        bool? Success { get; }

        /// <summary>
        /// Asynchronously waits for this future to be resolved within a maximum amount of time (and/or as long
        /// as the <paramref name="cancellation"/> is not signaled). 
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout in milliseconds to wait before returning false.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>True if <see cref="IsCompleted"/> is true, false if the timeout occurred before.</returns>
        Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default );

        /// <summary>
        /// Gets a task that can be awaited or be continued until this Future is on success or on error.
        /// This should be used with care since a Future describes a long running process: awaiting for it
        /// should be done when a resolution is known to happen shortly. 
        /// </summary>
        /// <returns>A task that is resolved when this Future is available.</returns>
        Task AsTask();
    }
}
