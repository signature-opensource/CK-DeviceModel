using CK.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public abstract partial class Device<TConfiguration>
    {
        // I'm a bit reluctant to use a static ConcurrentBag<Reminder> since this beast
        // adds a TLS entry and favors thread-bound instances (work stealing from other threads
        // invokes a lock).
        // Reminders in our async world on the tread pool have no thread affinity and won't be
        // needed really often (regarding the global job that must be done).
        // However, a shared pool seems a good idea and the single linked list used to acquire/release
        // is rather efficient: let's use a simple lock here (it won't be held for a long time). 
        // To avoid out-of-control expansion of the pool, rather than centrally managed this, each device
        // is alloted a MaxPooledReminderPerDevice (that may be configurable... but come on! it is set to 100):
        // each device tracks its own count and cannot ask for more than MaxPooledReminderPerDevice pooled Reminders.
        // This avoids a "bad device" to negatively impact the system (only the device that uses too much
        // reminders will "suffer").
        // The pool and the Reminder command are implemented below (outside of this generic type).

        /// <summary>
        /// Gets the maximum number of pooled reminder that a given device can use.
        /// When a device needs more reminders (at the same time), those reminders are not pooled.
        /// </summary>
        public static int MaxPooledReminderPerDevice => ReminderPool._maxPooledReminderPerDevice;

        /// <summary>
        /// Gets the current number of pooled reminder that are being used (out of the pool).
        /// </summary>
        public static int ReminderPoolInUseCount => ReminderPool._inUsePooledReminder;

        /// <summary>
        /// Gets the total number of pooled reminders.
        /// </summary>
        public static int ReminderPoolTotalCount => ReminderPool._totalPooledReminder;

        int _inUseReminderCount;

        Reminder AcquireReminder( DateTime time, object? state )
        {
            if( _inUseReminderCount++ < MaxPooledReminderPerDevice )
            {
                return ReminderPool.AcquireReminder( time, state );
            }
            _commandMonitor.Warn( $"The device '{FullName}' uses {_inUseReminderCount} reminders. MaxPooledReminderPerDevice is {MaxPooledReminderPerDevice}. The new Reminder will not be pooled." );
            return new Reminder( time, state, pooled: false );
        }

        void ReleaseReminder( Reminder c )
        {
            --_inUseReminderCount;
            if( c.Pooled ) ReminderPool.ReleaseReminder( c );
        }

        /// <summary>
        /// Registers a reminder in the delayed or immediate queue if the delay is too short to be delayed.
        /// The <paramref name="timeUtc"/> can be in the past (an immediate queuing will be done), but not
        /// in more than approximately 49 days (a <see cref="NotSupportedException"/> will be raised).
        /// <para>
        /// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
        /// eventually be called even if the device is stopped.
        /// </para>
        /// </summary>
        /// <param name="timeUtc">The time at which <see cref="OnReminderAsync"/> will be called.</param>
        /// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
        protected void AddReminder( DateTime timeUtc, object? state )
        {
            Throw.CheckArgument( timeUtc.Kind == DateTimeKind.Utc );
            var c = AcquireReminder( timeUtc, state );
            if( !EnqueueDelayed( c, true ) )
            {
                if( _commandQueueImmediate.Writer.TryWrite( c ) ) _commandQueue.Writer.TryWrite( CommandAwaker.Instance );
            }
        }

        /// <summary>
        /// Registers a reminder in the delayed or immediate queue if the delay is too short to be delayed.
        /// The <paramref name="timeUtc"/> can be in the past (an immediate queuing will be done), but not
        /// in more than approximately 49 days (a <see cref="NotSupportedException"/> will be raised).
        /// <para>
        /// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
        /// eventually be called even if the device is stopped.
        /// </para>
        /// </summary>
        /// <param name="delay">Positive time span.</param>
        /// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
        protected void AddReminder( TimeSpan delay, object? state ) => AddReminder( DateTime.UtcNow.Add( delay ), state );

        async Task HandleReminderCommandAsync( Reminder reminder, bool immediateHandling )
        {
            try
            {
                await OnReminderAsync( _commandMonitor, reminder.Time, reminder.State, immediateHandling ).ConfigureAwait( false );
            }
            catch( Exception ex )
            {
                using( _commandMonitor.OpenFatal( $"Unhandled error in OnReminderAsync. Stopping the device '{FullName}'.", ex ) )
                {
                    await HandleStopAsync( null, DeviceStoppedReason.SelfStoppedForceCall ).ConfigureAwait( false );
                }
            }
            finally
            {
                ReleaseReminder( reminder );
                _commandMonitor.Debug( $"ReminderPool: in use {ReminderPool._inUsePooledReminder} out of {ReminderPool._totalPooledReminder}." );
            }
        }

        /// <summary>
        /// Reminder callback triggered by <see cref="AddReminder(DateTime, object?)"/>.
        /// This does nothing at this level.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reminderTimeUtc">The exact time configured on the reminder.</param>
        /// <param name="state">The optional state.</param>
        /// <param name="immediateHandling">True if the reminder has not been delayed but posted to the immediate queue.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling ) => Task.CompletedTask;

    }

    sealed class Reminder : BaseDeviceCommand
    {
        public Reminder? _nextFreeReminder;
        public DateTime Time;
        public object? State;
        public readonly bool Pooled;

        public Reminder( DateTime time, object? state, bool pooled )
            : base( (string.Empty, null) )
        {
            Pooled = pooled;
            // SendTime is reset when the command is extracted from the _delayedQueue.
            // The Time field preserves it.
            _sendTime = Time = time;
            State = state;
            // A reminder is enqueued in the delayed queue (as soon as it is instantiated) but
            // this internal command must not be considered as a real long running command.
            // Moreover, if it cannot be enqueued (because it's in the past), it is enqueued as
            // an immediate command.
            TrySetLongRunningReason( null );
        }
        public override Type HostType => throw new NotImplementedException();
        internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
        public override string ToString() => nameof( Reminder );
    }

    static class ReminderPool
    {
        static object _lock = new object();
        static Reminder? _firstFreeReminder;

        // May be made settable one day.
        internal const int _maxPooledReminderPerDevice = 100;

        internal static int _totalPooledReminder;
        internal static int _inUsePooledReminder;

        public static Reminder AcquireReminder( DateTime time, object? state )
        {
            Reminder? c = null;
            lock( _lock )
            {
                c = _firstFreeReminder;
                if( c != null )
                {
                    _firstFreeReminder = c._nextFreeReminder;
                }
            }
            if( c != null )
            {
                c.Time = c._sendTime = time;
                c.State = state;
                Interlocked.Increment( ref _inUsePooledReminder );
                return c;
            }
            Interlocked.Increment( ref _totalPooledReminder );
            Interlocked.Increment( ref _inUsePooledReminder );
            return new Reminder( time, state, pooled: true );
        }

        public static void ReleaseReminder( Reminder c )
        {
            Debug.Assert( c.Pooled );
            lock( _lock )
            {
                c._nextFreeReminder = _firstFreeReminder;
                _firstFreeReminder = c;
            }
            Interlocked.Decrement( ref _inUsePooledReminder );
        }
    }


}
