using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// This is a <see cref="TaskCompletionSource{TResult}"/>-like object that allows exceptions or cancellations
    /// to be ignored (see <see cref="IAsyncCommand{TResult}"/> OnError and OnCanceled protected methods).
    /// </summary>
    public class CommandCompletionSource<TResult> : ICommandCompletionSource
    {
        readonly TaskCompletionSource<TResult> _tcs;
        readonly IAsyncCommand<TResult> _command;
        CKExceptionData? _exception;
        byte _state;

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource{TResult}"/>.
        /// </summary>
        public CommandCompletionSource( IAsyncCommand<TResult> command )
        {
            _tcs = new TaskCompletionSource<TResult>( TaskCreationOptions.RunContinuationsAsynchronously );
            _command = command ?? throw new ArgumentNullException( nameof( command ) );
        }

        /// <summary>
        /// Gets the command that holds this completion.
        /// See note in <see cref="CommandCompletionSource"/>.
        /// </summary>
        protected IAsyncCommand<TResult> AsyncCommand => _command;

        /// <summary>
        /// Gets the task that will be resolved when the command completes.
        /// </summary>
        public Task<TResult> Task => _tcs.Task;

        /// <inheritdoc />
        public CKExceptionData? Exception
        {
            get
            {
                if( _exception == null && _tcs.Task.IsFaulted )
                {
                    _exception = CKExceptionData.CreateFrom( _tcs.Task.Exception );
                }
                return _exception;
            }
        }

        Task ICommandCompletion.Task => _tcs.Task;

        /// <inheritdoc />
        public bool IsCompleted => _state != 0;

        /// <inheritdoc />
        public bool HasSucceed => (_state & 1) != 0;

        /// <inheritdoc />
        public bool HasFailed => (_state & 2) != 0;

        /// <inheritdoc />
        public bool HasBeenCanceled => (_state & 4) != 0;

        /// <summary>
        /// Transitions the <see cref="Task"/> into the <see cref="TaskStatus.RanToCompletion"/> state.
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>.
        /// </summary>
        /// <param name="result">The command result.</param>
        public void SetResult( TResult result )
        {
            _tcs.SetResult( result );
            _state |= 1;
        }

        /// <summary>
        /// Attempts to transition the <see cref="Task"/> into the <see cref="TaskStatus.Canceled"/> state.
        /// </summary>
        /// <param name="result">The command result.</param>
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        public bool TrySetResult( TResult result )
        {
            if( _tcs.TrySetResult( result ) )
            {
                _state |= 1;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Enables <see cref="IAsyncCommand{TResult}"/> OnError to
        /// transform exceptions into successful or canceled completion.
        /// </summary>
        public ref struct OnError
        {
            readonly CommandCompletionSource<TResult> _c;
            internal bool Try;
            internal bool Called;

            internal OnError( CommandCompletionSource<TResult> c, bool t )
            {
                _c = c;
                Try = t;
                Called = false;
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource.TrySetException(Exception)"/> has been called)
            /// the exception.
            /// The default <see cref="IAsyncCommand{TResult}"/> OnError method calls this method.
            /// </summary>
            /// <param name="ex">The exception to set.</param>
            public void SetException( Exception ex )
            {
                if( Called ) throw new InvalidOperationException( "OnError methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetException( ex );
                }
                else
                {
                    _c._tcs.SetException( ex );
                }
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource{TResult}.TrySetException(Exception)"/> has been called)
            /// a successful completion instead of an error.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource{TResult}.HasFailed"/> will be true: the fact that the command
            /// did not properly complete is available.
            /// </para>
            /// </summary>
            public void SetResult( TResult result )
            {
                if( Called ) throw new InvalidOperationException( "OnError methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetResult( result );
                }
                else
                {
                    _c._tcs.SetResult( result );
                }
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource{TResult}.TrySetException(Exception)"/> has been called)
            /// a cancellation completion instead of an error.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource{TResult}.HasFailed"/> will be true: the fact that the command
            /// did not properly complete is available.
            /// </para>
            /// </summary>
            public void SetCanceled()
            {
                if( Called ) throw new InvalidOperationException( "OnError methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetCanceled();
                }
                else
                {
                    _c._tcs.SetCanceled();
                }
            }

        }

        /// <inheritdoc />
        public void SetException( Exception exception )
        {
            var o = new OnError( this, false );
            _command.OnError( exception, ref o );
            if( !o.Called ) throw new InvalidOperationException( "One of the OnError methods must be called." );
            _state |= 2;
            if( !_tcs.Task.IsFaulted )
            {
                _exception = CKExceptionData.CreateFrom( exception );
            }
        }

        /// <inheritdoc />
        public bool TrySetException( Exception exception )
        {
            var o = new OnError( this, true );
            _command.OnError( exception, ref o );
            if( !o.Called ) throw new InvalidOperationException( "One of the OnError methods must be called." );
            if( o.Try )
            {
                _state |= 2;
                if( !_tcs.Task.IsFaulted )
                {
                    _exception = CKExceptionData.CreateFrom( exception );
                }
            }
            return o.Try;
        }

        /// <summary>
        /// Enables <see cref="IAsyncCommand{TResult}"/> OnCanceled method to
        /// transform exceptions into successful or canceled completion.
        /// </summary>
        public ref struct OnCanceled
        {
            readonly CommandCompletionSource<TResult> _c;
            internal bool Try;
            internal bool Called;

            internal OnCanceled( CommandCompletionSource<TResult> c, bool t )
            {
                _c = c;
                Try = t;
                Called = false;
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource{TResult}.TrySetCanceled()"/> has been called)
            /// a successful instead of a cancellation completion.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource{TResult}.HasBeenCanceled"/> will be true: the fact that the command
            /// has been canceled is available.
            /// </para>
            /// </summary>
            public void SetResult( TResult result )
            {
                if( Called ) throw new InvalidOperationException( "OnCanceled methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetResult( result );
                }
                else
                {
                    _c._tcs.SetResult( result );
                }
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource{TResult}.TrySetCanceled()"/> has been called)
            /// the cancellation completion.
            /// The default <see cref="IAsyncCommand{TResult}"/> OnCanceled method calls this method.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource{TResult}.HasBeenCanceled"/> will be true: the fact that the command
            /// has been canceled is available.
            /// </para>
            /// </summary>
            public void SetCanceled()
            {
                if( Called ) throw new InvalidOperationException( "OnCanceled methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetCanceled();
                }
                else
                {
                    _c._tcs.SetCanceled();
                }
            }
        }

        /// <inheritdoc />
        public void SetCanceled()
        {
            var o = new OnCanceled( this, false );
            _command.OnCanceled( ref o );
            if( !o.Called ) throw new InvalidOperationException( "One of the OnCanceled methods must be called." );
            _state |= 4;
        }

        /// <inheritdoc />
        public bool TrySetCanceled()
        {
            var o = new OnCanceled( this, true );
            _command.OnCanceled( ref o );
            if( !o.Called ) throw new InvalidOperationException( "One of the OnCanceled methods must be called." );
            if( o.Try ) _state |= 4;
            return o.Try;
        }

        /// <summary>
        /// Overridden to return the current completion status.
        /// </summary>
        /// <returns>The current status.</returns>
        public override string ToString() => CommandCompletionSource.GetStatus( Task.Status, _state );

    }
}
