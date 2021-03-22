using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    /// <summary>
    /// <see cref="Task{TResult}"/> like object that encapsulates the deferred result of a <see cref="DeviceCommand{TResult}"/>.
    /// This can be awaited, just like Task, and exposes a kind of "Railway Oriented Programming" interface by using Nullable Reference Types
    /// on <see cref="Error"/> and <see cref="Value"/>.
    /// </summary>
    public class CommandCompletion<TResult> : ICommandCompletion
    {
        readonly TaskCompletionSource<object?> _tcs = new();

        /// <inheritdoc />
        public Exception? Error => _tcs.Task.IsCompletedSuccessfully
                                            ? _tcs.Task.Result as Exception
                                            : null;

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( Error ) )]
        public bool HasError => Error != null;

        /// <inheritdoc />
        public bool IsCompleted => _tcs.Task.IsCompletedSuccessfully;

        /// <inheritdoc />
        public bool? Success => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result is not Exception
                                    : null;

        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and a <see cref="Value"/> is available.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Value ) )]
        public bool HasValue => _tcs.Task.IsCompletedSuccessfully
                                ? _tcs.Task.Result is not Exception
                                : false;

        /// <summary>
        /// Gets the final result.
        /// This must be called when and only when <see cref="HasValue"/> is true
        /// otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        [MaybeNull]
        public TResult Value => _tcs.Task.IsCompletedSuccessfully && _tcs.Task.Result is not Exception
                                ? (TResult)_tcs.Task.Result
                                : throw new InvalidOperationException( "CommandCompletion<TResult>.Value must be accessed only when HasValue or Success are true." );

        /// <inheritdoc />
        public Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default ) => _tcs.Task.WaitAsync( millisecondsTimeout, cancellation );

        /// <summary>
        /// Enables this <see cref="CommandCompletion{TResult}"/> to support await operator.
        /// </summary>
        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly TaskAwaiter<object?> _a;

            internal Awaiter( TaskAwaiter<object?> a ) => _a = a;

            /// <summary>
            /// Gets a whether the asynchronous task has completed.
            /// </summary>
            public bool IsCompleted => _a.IsCompleted;

            /// <summary>
            /// Gets the final result value on completion.
            /// </summary>
            /// <returns>The result.</returns>
            public TResult GetResult() => (TResult)_a.GetResult();

            /// <inheritdoc />
            public void OnCompleted( Action continuation ) => _a.OnCompleted( continuation );

            /// <inheritdoc />
            public void UnsafeOnCompleted( Action continuation ) => _a.UnsafeOnCompleted( continuation );
        }

        /// <summary>
        /// Enables this CommandCompletion to be awaited for its value, just as a Task.
        /// </summary>
        /// <returns>The awaitable.</returns>
        public Awaiter GetAwaiter() => new Awaiter( _tcs.Task.GetAwaiter() );

        ICriticalNotifyCompletion ICommandCompletion.GetAwaiter() => GetAwaiter();

        /// <summary>
        /// Sets the successful result.
        /// Can be called only once and only if <see cref="SetError(Exception)"/> nor <see cref="SetCanceled()"/> have been called yet.
        /// </summary>
        /// <param name="result">The final, successful, result.</param>
        public void SetSuccess( TResult result ) => _tcs.SetResult( result );

        /// <summary>
        /// Sets an error.
        /// Can be called only once and only if <see cref="SetSuccess(TResult)"/> nor <see cref="SetCanceled()"/> have been called yet.
        /// </summary>
        /// <param name="error">The exception.</param>
        public void SetError( Exception error )
        {
            _tcs.SetResult( error ?? throw new ArgumentNullException( nameof( error ) ) );
        }

        /// <summary>
        /// Sets a cancellation error: the <see cref="Error"/> will be a <see cref="OperationCanceledException"/>.
        /// Can be called only once and only if <see cref="SetSuccess(TResult)"/> nor <see cref="SetError(Exception)"/> have been called yet.
        /// </summary>
        public void SetCanceled()
        {
            _tcs.SetResult( CommandCompletion._operationCanceledException );
        }

    }
}
