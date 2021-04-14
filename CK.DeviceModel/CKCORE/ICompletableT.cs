using System;

namespace CK.Core
{
    /// <summary>
    /// Abstraction of a command with result that holds its <see cref="Completion"/>.
    /// <para>
    /// The protected OnError and OnCanceled methods enable the error and cancel
    /// strategies of the <see cref="CompletionSource{TResult}"/> to be provided by the command itself.
    /// </para>
    /// </summary>
    public interface ICompletable<TResult>
    {
        /// <summary>
        /// Gets the completion of this command.
        /// </summary>
        ICompletion<TResult> Completion { get; }

        /// <summary>
        /// Called by the <see cref="Completion"/> when a error is set.
        /// This default implementation calls <see cref="CompletionSource{TResult}.OnError.SetException(Exception)"/>.
        /// </summary>
        /// <param name="ex">The error.</param>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnError( Exception ex, ref CompletionSource<TResult>.OnError result ) => result.SetException( ex );

        /// <summary>
        /// Called by the <see cref="Completion"/> when a cancellation occurred.
        /// This default implementation calls <see cref="CompletionSource{TResult}.OnCanceled.SetCanceled()"/>.
        /// </summary>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnCanceled( ref CompletionSource<TResult>.OnCanceled result ) => result.SetCanceled();
    }
}
