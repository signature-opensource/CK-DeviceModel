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
    /// A future is a little bit like a Task except that it is not directly awaitable: you can <see cref="WaitAsync(int, CancellationToken)"/> a future
    /// with a given timeout and a cancellation token, but cannot await or set a continuation directly on it.
    /// <para>
    /// This is to be used for long running processes that are typically fully asynchronous and/or externally implemented. 
    /// </para>
    /// </summary>
    public class Future : IFuture
    {
        readonly TaskCompletionSource<CKExceptionData?> _tcs = new TaskCompletionSource<CKExceptionData?>();

        /// <inheritdoc />
        public Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default ) => _tcs.Task.WaitAsync( millisecondsTimeout, cancellation );

        /// <inheritdoc />
        public bool IsCompleted => _tcs.Task.IsCompletedSuccessfully;

        /// <inheritdoc />
        public CKExceptionData? Error => _tcs.Task.IsCompletedSuccessfully
                                            ? _tcs.Task.Result
                                            : null;

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( Error ) )]
        public bool HasError => Error != null;

        /// <inheritdoc />
        public bool? Success => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result == null
                                    : null;

        /// <inheritdoc />
        public Task AsTask() => _tcs.Task;

        /// <summary>
        /// Sets the successfut result.
        /// Can be called only once and only if <see cref="SetError(CKExceptionData)"/> has not been called.
        /// </summary>
        /// <param name="result">The final, successful, result.</param>
        public void SetSuccess() => _tcs.SetResult( null );

        /// <summary>
        /// Sets a captured error.
        /// Can be called only once and only if <see cref="SetSuccess()"/> has not been called.
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
