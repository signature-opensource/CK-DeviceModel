using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// This is a <see cref="TaskCompletionSource{TResult}"/>-like object that allows exceptions or cancellations
    /// to be transformed into special results: see <see cref="IgnoreCanceled"/> and <see cref="IgnoreException"/>.
    /// </summary>
    public class CommandCompletionSource<TResult> : ICommandCompletionSource
    {
        readonly TaskCompletionSource<TResult> _tcs;
        readonly Func<Exception,TResult>? _errorTransform;
        readonly TResult? _cancelResult;
        byte _state;

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource{TResult}"/> that keeps the exception object
        /// and the canceled state in the <see cref="Task"/>.
        /// </summary>
        public CommandCompletionSource() => _tcs = new TaskCompletionSource<TResult>( TaskCreationOptions.RunContinuationsAsynchronously );

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource"/> that transforms any exception or cancellation into
        /// a specific result in the <see cref="Task"/>: the Task will always be <see cref="TaskStatus.RanToCompletion"/>.
        /// </summary>
        /// <param name="errorOrCancelResult">Result that will replace error or cancellation.</param>
        public CommandCompletionSource( TResult errorOrCancelResult )
        {
            _tcs = new TaskCompletionSource<TResult>( TaskCreationOptions.RunContinuationsAsynchronously );
            _errorTransform = _ => errorOrCancelResult;
            _cancelResult = errorOrCancelResult;
            _state = 3;
        }

        /// <summary>
        /// Creates a <see cref="CommandCompletionSource"/> that can transform any exception or cancellation into
        /// specific results in the <see cref="Task"/>: the Task will always be <see cref="TaskStatus.RanToCompletion"/>.
        /// </summary>
        /// <param name="transformError">Optional transformation from the error to a result.</param>
        /// <param name="ignoreCanceled">True to transform cancellation into <paramref name="ignoreCanceled"/> result value.</param>
        /// <param name="cancelResult">The result to use on cancellation. Used only is <paramref name="ignoreCanceled"/> is true.</param>
        public CommandCompletionSource( Func<Exception, TResult>? transformError, bool ignoreCanceled, TResult cancelResult )
        {
            _tcs = new TaskCompletionSource<TResult>( TaskCreationOptions.RunContinuationsAsynchronously );
            if( transformError != null )
            {
                _errorTransform = transformError;
                _state = 1;
            }
            if( ignoreCanceled )
            {
                _cancelResult = cancelResult;
                _state |= 2;
            }
        }

        /// <summary>
        /// Gets the task that will be resolved when the command completes.
        /// </summary>
        public Task<TResult> Task => _tcs.Task;

        Task ICommandCompletion.Task => _tcs.Task;

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
        /// <param name="result">The command result.</param>
        public void SetResult( TResult result )
        {
            _tcs.SetResult( result );
            _state |= 4;
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
                _tcs.SetResult( _errorTransform!( exception ) );
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
            bool r = IgnoreException ? _tcs.TrySetResult( _errorTransform!( exception ) ) : _tcs.TrySetException( exception );
            if( r ) _state |= 8;
            return r;
        }

        /// <inheritdoc />
        public void SetCanceled()
        {
            if( IgnoreCanceled )
            {
                _tcs.SetResult( _cancelResult! );
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
            bool r = IgnoreCanceled ? _tcs.TrySetResult( _cancelResult! ) : _tcs.TrySetCanceled();
            if( r ) _state |= 16;
            return r;
        }

        /// <summary>
        /// Overridden to return the current status and configuration.
        /// </summary>
        /// <returns>The current status and configuration.</returns>
        public override string ToString() => CommandCompletionSource.GetStatus( _state );

    }
}
