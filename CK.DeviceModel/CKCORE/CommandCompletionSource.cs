using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// This is a TaskCompletionSource-like object that allows exceptions or cancellations
    /// to be ignored (see <see cref="IAsyncCommand.OnError(Exception, ref OnError)"/>) and
    /// <see cref="IAsyncCommand.OnCanceled(ref OnCanceled)"/>.
    /// <para>
    /// This is the counterpart of the <see cref="CommandCompletionSource{TResult}"/> that can transform
    /// errors or cancellations into specific results.
    /// It's much more useful than this one, but for the sake of symmetry, this "no result" class exposes the same functionalities.
    /// </para>
    /// </summary>
    public class CommandCompletionSource : ICommandCompletionSource
    {
        /// Adapter waiting for .Net 5 TaskCompletionSource.
        readonly TaskCompletionSource<object?> _tcs;
        readonly IAsyncCommand _command;
        byte _state;

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource"/>.
        /// </summary>
        /// <param name="command">The command that exposes this completion.</param>
        public CommandCompletionSource( IAsyncCommand command )
        {
            _tcs = new TaskCompletionSource<object?>( TaskCreationOptions.RunContinuationsAsynchronously );
            _command = command ?? throw new ArgumentNullException( nameof(command) );
        }

        /// <inheritdoc />
        public Task Task => _tcs.Task;

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
        public void SetResult()
        {
            _tcs.SetResult( null );
            _state |= 1;
        }

        /// <summary>
        /// Attempts to transition the <see cref="Task"/> into the <see cref="TaskStatus.RanToCompletion"/> state.
        /// </summary>
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        public bool TrySetResult()
        {
            if( _tcs.TrySetResult( null ) )
            {
                _state |= 1;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Enables <see cref="IAsyncCommand.OnError(Exception, ref OnError)"/> to
        /// transform exceptions into successful or canceled completion.
        /// </summary>
        public ref struct OnError
        {
            readonly CommandCompletionSource _c;
            internal bool Try;
            internal bool Called;

            internal OnError( CommandCompletionSource c, bool t )
            {
                _c = c;
                Try = t;
                Called = false;
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource.TrySetException(Exception)"/> has been called)
            /// the exception.
            /// The default <see cref="IAsyncCommand.OnError(Exception, ref OnError)"/> calls this method.
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
            /// Sets (or tries to set if <see cref="CommandCompletionSource.TrySetException(Exception)"/> has been called)
            /// a successful completion instead of an error.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource.HasFailed"/> will be true: the fact that the command
            /// did not properly complete is available.
            /// </para>
            /// </summary>
            public void SetResult()
            {
                if( Called ) throw new InvalidOperationException( "OnError methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetResult( null );
                }
                else
                {
                    _c._tcs.SetResult( null );
                }
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="CommandCompletionSource.TrySetException(Exception)"/> has been called)
            /// a cancellation completion instead of an error.
            /// <para>
            /// Note that the <see cref="CommandCompletionSource.HasFailed"/> will be true: the fact that the command
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
        }

        /// <inheritdoc />
        public bool TrySetException( Exception exception )
        {
            var o = new OnError( this, true );
            _command.OnError( exception, ref o );
            if( !o.Called ) throw new InvalidOperationException( "One of the OnError methods must be called." );
            if( o.Try ) _state |= 2;
            return o.Try;
        }

        /// <summary>
        /// Enables <see cref="IAsyncCommand.OnError(Exception, ref OnError)"/> to
        /// transform exceptions into successful or canceled completion.
        /// </summary>
        public ref struct OnCanceled
        {
            readonly CommandCompletionSource _c;
            internal bool Try;
            internal bool Called;

            internal OnCanceled( CommandCompletionSource c, bool t )
            {
                _c = c;
                Try = t;
                Called = false;
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="TrySetCanceled()"/> has been called)
            /// a successful instead of a cancellation completion.
            /// <para>
            /// Note that the <see cref="HasBeenCanceled"/> will be true: the fact that the command
            /// has been canceled is available.
            /// </para>
            /// </summary>
            public void SetResult()
            {
                if( Called ) throw new InvalidOperationException( "OnCanceled methods must be called only once." );
                Called = true;
                if( Try )
                {
                    Try = _c._tcs.TrySetResult( null );
                }
                else
                {
                    _c._tcs.SetResult( null );
                }
            }

            /// <summary>
            /// Sets (or tries to set if <see cref="TrySetCanceled()"/> has been called)
            /// the cancellation completion.
            /// The <see cref="IAsyncCommand"/> OnCanceled method default implementation calls this method.
            /// <para>
            /// Note that the <see cref="HasBeenCanceled"/> will be true: the fact that the command
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
        public override string ToString() => GetStatus( _tcs.Task.Status, _state );

        static internal string GetStatus( TaskStatus t, byte s )
        {
            return t switch
            {
                TaskStatus.RanToCompletion => (s & 2) != 0
                                                ? "Completed (HasFailed)"
                                                : (s & 4) != 0
                                                    ? "Completed (HasBeenCanceled)"
                                                    : "Success",
                TaskStatus.Canceled => (s & 2) != 0
                                        ? "Canceled (HasFailed)"
                                        : "Canceled",
                TaskStatus.Faulted => "Failed",
                _ => "Waiting"
            };
        }
    }
}
