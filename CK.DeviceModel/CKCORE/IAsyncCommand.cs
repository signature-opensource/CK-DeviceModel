using System;

namespace CK.Core
{
    /// <summary>
    /// Abstraction of a command without result that holds its <see cref="Completion"/>.
    /// <para>
    /// The protected OnError and OnCanceled enable the error and cancel strategies of
    /// the <see cref="CommandCompletionSource"/> to be provided by the command itself.
    /// </para>
    /// </summary>
    public interface IAsyncCommand
    {
        /// <summary>
        /// Gets the <see cref="CommandCompletionSource"/> for this command.
        /// </summary>
        CommandCompletionSource Completion { get; }

        /// <summary>
        /// Called by the <see cref="Completion"/> when a error is set.
        /// This default implementation calls <see cref="CommandCompletionSource.OnError.SetException(Exception)"/>.
        /// </summary>
        /// <param name="ex">The error.</param>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnError( Exception ex, ref CommandCompletionSource.OnError result ) => result.SetException( ex );

        /// <summary>
        /// Called by the <see cref="Completion"/> when a cancellation occurred.
        /// This default implementation calls <see cref="CommandCompletionSource.OnCanceled.SetCanceled()"/>.
        /// </summary>
        /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
        internal protected void OnCanceled( ref CommandCompletionSource.OnCanceled result ) => result.SetCanceled();

    }
}
