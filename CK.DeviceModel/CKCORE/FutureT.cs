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
    /// <para>
    /// Just like the Future implementation, this should not be exposed directly
    /// only the <see cref="IFuture{T}"/> should be exposed to benefit from its covariance
    /// with <typeparamref name="T"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public class Future<T> : IFuture, IFuture<T>
    {
        readonly TaskCompletionSource<object?> _tcs = new TaskCompletionSource<object?>();

        /// <inheritdoc />
        public Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default ) => _tcs.Task.WaitAsync( millisecondsTimeout, cancellation );

        /// <inheritdoc />
        public bool IsCompleted => _tcs.Task.IsCompletedSuccessfully;

        /// <inheritdoc />
        public CKExceptionData? Error => _tcs.Task.IsCompletedSuccessfully
                                            ? _tcs.Task.Result as CKExceptionData
                                            : null;

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( Error ) )]
        public bool HasError => Error != null;

        /// <inheritdoc />
        [MaybeNull]
        public T Value => _tcs.Task.IsCompletedSuccessfully && _tcs.Task.Result is T v
                            ? v
                            : throw new InvalidOperationException( "Future<T>.Value must be accessed only when Success is true." );

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( Value ) )]
        public bool HasValue => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result is T
                                    : false;

        /// <inheritdoc />
        public bool? Success => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result is T
                                    : null;

        /// <inheritdoc />
        public Task AsTask() => _tcs.Task;

        /// <summary>
        /// Sets the successful result.
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
