using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    public abstract partial class Device<TConfiguration>
    {
        /// <summary>
        /// Registers a reminder that must be in the future or, by default, an <see cref="ArgumentException"/> is thrown.
        /// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
        /// eventually be called even if the device is stopped.
        /// </summary>
        /// <param name="timeUtc">The time in the future at which <see cref="OnReminderAsync"/> will be called.</param>
        /// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
        /// <param name="throwIfPast">False to returns false instead of throwing an ArgumentExcetion if the reminder cannot be registered.</param>
        /// <returns>True on success or false if the reminder cannot be set and <paramref name="throwIfPast"/> is false.</returns>
        protected bool AddReminder( DateTime timeUtc, object? state, bool throwIfPast = true )
        {
            Throw.CheckArgument( timeUtc.Kind == DateTimeKind.Utc );
            var c = new Reminder( timeUtc, state );
            if( !EnqueueDelayed( c ) )
            {
                if( throwIfPast ) Throw.ArgumentException( nameof( timeUtc ), $"Must be in the future: value '{timeUtc:O}' is before now '{DateTime.UtcNow:O}'." );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Registers a reminder that must be in the future or, by default, an <see cref="ArgumentException"/> is thrown.
        /// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
        /// eventually be called even if the device is stopped.
        /// </summary>
        /// <param name="delay">Positive time span.</param>
        /// <param name="state">An optional state that will be provided to OnReminderAsync.</param>
        /// <param name="throwIfPast">False to returns false instead of throwing an ArgumentExcetion if the reminder cannot be registered.</param>
        /// <returns>True on success or false if the reminder cannot be set and <paramref name="throwIfPast"/> is false.</returns>
        protected bool AddReminder( TimeSpan delay, object? state, bool throwIfPast = true ) => AddReminder( DateTime.UtcNow.Add( delay ), state, throwIfPast );

        /// <summary>
        /// Reminder callback triggered by <see cref="AddReminder(DateTime, object?, bool)"/>.
        /// This does nothing at this level.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reminderTimeUtc">The exact time configured on the reminder.</param>
        /// <param name="state">The optional state.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state ) => Task.CompletedTask;

    }

    // Avoid the Device<TConfiguration> generics host.
    sealed class Reminder : BaseDeviceCommand
    {
        public readonly DateTime Time;
        public readonly object? State;
        public Reminder( DateTime time, object? state )
            : base( (string.Empty, null) )
        {
            // SendTime is reset when the command is extracted from the _delayedQueue.
            // Time field preserves it.
            _sendTime = Time = time;
            State = state;
            // A reminder is enqueued in the delayed queue (as soon as it is instantiated) but
            // this internal command must not be considered as a real long running command.
            TrySetLongRunningReason( null );
        }
        public override Type HostType => throw new NotImplementedException();
        internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
        public override string ToString() => nameof( Reminder );
    }

}
