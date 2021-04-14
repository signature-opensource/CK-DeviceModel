using System;

namespace CK.Core
{
    /// <summary>
    /// Abstraction of a command without result that holds its <see cref="Completion"/>.
    /// <para>
    /// The protected OnError and OnCanceled enable the error and cancel strategies of
    /// the <see cref="CompletionSource"/> to be provided by the command itself.
    /// </para>
    /// </summary>
    public interface ICompletable
    {
        /// <summary>
        /// Gets the completion of this command.
        /// </summary>
        ICompletion Completion { get; }

        /// <summary>
        /// Called by the <see cref="Completion"/> when a error is set.
        /// This default implementation calls <see cref="CompletionSource.OnError.SetException(Exception)"/>.
        /// </summary>
        /// <param name="ex">The error.</param>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnError( Exception ex, ref CompletionSource.OnError result ) => result.SetException( ex );

        /// <summary>
        /// Called by the <see cref="Completion"/> when a cancellation occurred.
        /// This default implementation calls <see cref="CompletionSource.OnCanceled.SetCanceled()"/>.
        /// </summary>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetCanceled();

    }
}
