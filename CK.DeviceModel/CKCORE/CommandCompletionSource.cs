using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// This is a TaskCompletionSource-like object that allows exceptions or cancellations
    /// to be ignored: see <see cref="IgnoreCanceled"/> and <see cref="IgnoreException"/>.
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
        byte _state;

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource"/> that keeps the exception object
        /// and the canceled state in the <see cref="Task"/>.
        /// </summary>
        public CommandCompletionSource() => _tcs = new TaskCompletionSource<object?>( TaskCreationOptions.RunContinuationsAsynchronously );

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource"/> that ignores the exception object
        /// and the canceled state in the <see cref="Task"/>: the Task will always be <see cref="TaskStatus.RanToCompletion"/>.
        /// </summary>
        /// <param name="ignoreException">True to ignore errors.</param>
        /// <param name="ignoreCanceled">True to ignore cancellation.</param>
        public CommandCompletionSource( bool ignoreException, bool ignoreCanceled )
        {
            _tcs = new TaskCompletionSource<object?>( TaskCreationOptions.RunContinuationsAsynchronously );
            if( ignoreException ) _state = 1;
            if( ignoreCanceled ) _state |= 2;
        }

        /// <inheritdoc />
        public Task Task => _tcs.Task;

        /// <inheritdoc />
        public bool IgnoreException => (_state & 1) != 0;

        /// <inheritdoc />
        public bool IgnoreCanceled => (_state & 2) != 0;

        /// <inheritdoc />
        public bool IsCompleted => (_state & (4 | 8 | 16)) != 0;

        /// <inheritdoc />
        public bool IsSuccessful => (_state & 4) != 0;

        /// <inheritdoc />
        public bool IsError => (_state & 8) != 0;

        /// <inheritdoc />
        public bool IsCanceled => (_state & 16) != 0;

        /// <summary>
        /// Transitions the <see cref="Task"/> into the <see cref="TaskStatus.RanToCompletion"/> state.
        /// An <see cref="InvalidOperationException"/> is thrown if Task is already in one of the three final
        /// states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/>.
        /// </summary>
        public void SetResult()
        {
            _tcs.SetResult( null );
            _state |= 4;
        }

        /// <summary>
        /// Attempts to transition the <see cref="Task"/> into the <see cref="TaskStatus.Canceled"/> state.
        /// </summary>
        /// <returns>
        /// True if the operation was successful; false if the operation was unsuccessful.
        /// </returns>
        public bool TrySetResult()
        {
            if( _tcs.TrySetResult( null ) )
            {
                _state |= 4;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public void SetException( Exception exception )
        {
            if( IgnoreException )
            {
                _tcs.SetResult( null );
            }
            else
            {
                _tcs.SetException( exception );
            }
            _state |= 8;
        }

        /// <inheritdoc />
        public bool TrySetException( Exception exception )
        {
            bool r = IgnoreException ? _tcs.TrySetResult( null ) : _tcs.TrySetException( exception );
            if( r ) _state |= 8;
            return r;
        }

        /// <inheritdoc />
        public void SetCanceled()
        {
            if( IgnoreCanceled )
            {
                _tcs.SetResult( null );
            }
            else
            {
                _tcs.SetCanceled();
            }
            _state |= 16;
        }

        /// <inheritdoc />
        public bool TrySetCanceled()
        {
            bool r = IgnoreCanceled ? _tcs.TrySetResult( null ) : _tcs.TrySetCanceled();
            if( r ) _state |= 16;
            return r;
        }

        /// <summary>
        /// Overridden to return the current status and configuration.
        /// </summary>
        /// <returns>The current status and configuration.</returns>
        public override string ToString() => GetStatus( _state );

        static readonly string[] _ignores = new[]
        {
            "",
            " (IgnoreException)",
            " (IgnoreCancel)",
            " (IgnoreException, IgnoreCancel)"
        };

        static internal string GetStatus( byte s )
        {
            string r;
            if( (s & 4) != 0 ) r = "Success";
            else if( (s & 8) != 0 ) r = "Exception";
            else if( (s & 16) != 0 ) r = "Canceled";
            else r = "Waiting";
            return r + _ignores[s & 3];
        }
    }
}
