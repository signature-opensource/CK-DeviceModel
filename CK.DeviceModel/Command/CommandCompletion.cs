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
    /// <see cref="Task"/> like object that encapsulates the deferred completion of a <see cref="DeviceCommand"/> without result.
    /// This can be awaited, just like Task, and exposes a kind of "Railway Programming" interface by using Nullable Reference Type.
    /// </summary>
    public class CommandCompletion : ICommandCompletion
    {
        readonly TaskCompletionSource<Exception?> _tcs = new();
        internal static readonly OperationCanceledException _operationCanceledException = new OperationCanceledException();

        /// <inheritdoc />
        public Exception? Error => _tcs.Task.IsCompletedSuccessfully
                                            ? _tcs.Task.Result
                                            : null;

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( Error ) )]
        public bool HasError => Error != null;

        /// <inheritdoc />
        public bool IsCompleted => _tcs.Task.IsCompletedSuccessfully;

        /// <inheritdoc />
        public bool? Success => _tcs.Task.IsCompletedSuccessfully
                                    ? _tcs.Task.Result == null
                                    : null;

        /// <inheritdoc />
        public Task<bool> WaitAsync( int millisecondsTimeout, CancellationToken cancellation = default ) => _tcs.Task.WaitAsync( millisecondsTimeout, cancellation );

        /// <summary>
        /// Enables this CommandCompletion to be awaited, just as a Task.
        /// </summary>
        /// <returns>The awaitable.</returns>
        public TaskAwaiter GetAwaiter() => ((Task)_tcs.Task).GetAwaiter();

        /// <summary>
        /// Sets the successful result.
        /// Can be called only once and only if <see cref="SetError(Exception)"/> nor <see cref="SetCanceled()"/> have been called yet.
        /// </summary>
        /// <param name="result">The final, successful, result.</param>
        public void SetSuccess() => _tcs.SetResult( null );

        /// <summary>
        /// Sets an error.
        /// Can be called only once and only if <see cref="SetSuccess()"/> nor <see cref="SetCanceled()"/> have been called yet.
        /// </summary>
        /// <param name="error">The exception.</param>
        public void SetError( Exception error )
        {
            _tcs.SetResult( error ?? throw new ArgumentNullException( nameof( error ) ) );
        }

        /// <summary>
        /// Sets a cancellation error: the <see cref="Error"/> will be a <see cref="OperationCanceledException"/>.
        /// Can be called only once and only if <see cref="SetSuccess()"/> nor <see cref="SetError(Exception)"/> have been called yet.
        /// </summary>
        public void SetCanceled()
        {
            _tcs.SetResult( _operationCanceledException );
        }

    }
}
