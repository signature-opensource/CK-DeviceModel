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
    /// Implementation of <see cref="IFuture"/>.
    /// This should be used only on the implementation side: the IFuture interface must
    /// be exposed so that it can be easily replaced (using name hiding,
    /// see https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/new-modifier) to
    /// extend a base class.
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
        /// Sets the successful result.
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
