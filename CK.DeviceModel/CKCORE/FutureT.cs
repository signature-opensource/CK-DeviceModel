using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Implementation of a <see cref="Future"/> with a <see cref="Value"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public class Future<T>
    {
        readonly TaskCompletionSource<object?> _tcs = new TaskCompletionSource<object?>();

        /// <summary>
        /// Asynchronously waits for this result to be available within a maximum amount of time (and/or as long
        /// as the <paramref name="cancellation"/> is not signaled). 
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout in milliseconds to wait before returning false.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>True if <see cref="IsCompleted"/> is true, false if the timeout occurred before.</returns>
        public Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default ) => _tcs.Task.WaitAsync( millisecondsTimeout, cancellation );

        /// <summary>
        /// Gets whether the <see cref="Value"/> or the <see cref="Error"/> is available.
        /// </summary>
        public bool IsCompleted => _tcs.Task.IsCompletedSuccessfully;

        /// <summary>
        /// Gets the error if an error occurred.
        /// This is null as long as <see cref="IsCompleted"/> is false or if <see cref="Success"/> is true.
        /// </summary>
        public CKExceptionData? Error => _tcs.Task.IsCompletedSuccessfully
                                            ? _tcs.Task.Result as CKExceptionData
                                            : null;

        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and an <see cref="Error"/> occurred.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Error ) )]
        public bool HasError => Error != null;

        /// <summary>
        /// Gets the final result.
        /// This must be called when and only when <see cref="HasValue"/> is true
        /// otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        [MaybeNull]
        public T Value => _tcs.Task.IsCompletedSuccessfully && _tcs.Task.Result is T v 
                            ? v
                            : throw new InvalidOperationException( "Future<T>.Value must be accessed only when Success is true." );

        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and a <see cref="Value"/> is available.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Value ) )]
        public bool HasValue => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result is T
                                    : false;

        /// <summary>
        /// Gets whether the result is not yet available (null when <see cref="IsCompleted"/> is false),
        /// whether an error occurred (false when <see cref="HasError"/> is true),
        /// or whether the command has been sucessfully executed: <see cref="HasValue"/> is true.
        /// </summary>
        public bool? Success => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result is T
                                    : null;

        /// <summary>
        /// Sets the successfut result.
        /// Can be called only once and only if <see cref="SetError(CKExceptionData)"/> has not been called.
        /// </summary>
        /// <param name="result">The final, successful, result.</param>
        public void SetSuccess( T result ) => _tcs.SetResult( result );

        /// <summary>
        /// Sets a captured error.
        /// Can be called only once and only if <see cref="SetSuccess(T)"/> has not been called.
        /// </summary>
        /// <param name="error">The error.</param>
        public void SetError( CKExceptionData error ) => _tcs.SetResult( error );

        /// <summary>
        /// Sets an error. <see cref="CKExceptionData.CreateFrom(Exception)"/> is called.
        /// Can be called only once and only if <see cref="SetSuccess(T)"/> has not been called.
        /// </summary>
        /// <param name="error">The exception.</param>
        public void SetError( Exception error ) => _tcs.SetResult( CKExceptionData.CreateFrom( error ) );
    }

}
