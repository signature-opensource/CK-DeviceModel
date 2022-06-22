using CK.Core;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base command class that exposes the host that must handle it.
    /// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost}"/>
    /// must be used, or the <see cref="DeviceCommand{THost,TResult}"/> when the command generates
    /// a result.
    /// </summary>
    public abstract class BaseDeviceCommand
    {
        /// <summary>
        /// Cancellation reason used when <see cref="ICompletionSource.SetCanceled()"/> or <see cref="ICompletionSource.TrySetCanceled()"/>
        /// have been used.
        /// </summary>
        public const string CommandCompletionCanceledReason = "CommandCompletionCanceled";

        /// <summary>
        /// Cancellation reason used when the timeout computed by <see cref="Device{TConfiguration}.GetCommandTimeoutAsync(IActivityMonitor, BaseDeviceCommand)"/>
        /// elapsed.
        /// </summary>
        public const string CommandTimeoutReason = "CommandTimeout";

        /// <summary>
        /// Cancellation reason used when the token provided to <see cref="Device{TConfiguration}.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, bool, CancellationToken)"/>
        /// has been signaled.
        /// </summary>
        public const string SendCommandTokenReason = "SendCommandToken";

        /// <summary>
        /// The command is long running because the device is stopped. See <see cref="StoppedBehavior"/>.
        /// </summary>
        public const string LongRunningDeferredReason = "Deferred";

        /// <summary>
        /// The command is long running because it is delayed. See <see cref="SendingTimeUtc"/>.
        /// </summary>
        public const string LongRunningDelayedReason = "Delayed";

        /// <summary>
        /// The command is long running because it has not been completed by its handling.
        /// </summary>
        public const string LongRunningWaitForCompletionReason = "WaitForCompletion";

        string _deviceName;
        string? _controllerKey;
        // Internal for the command queue.
        internal DateTime _sendTime;
        CancellationTokenRegistration[] _cancels;
        readonly TaskCompletionSource<string?> _longRunningReason;
        static readonly Action<object?> _cancelFromTokenHandle = o => CancelFromTokenRelay( o! );
        static readonly Action<object?> _cancelFromTimeoutHandle = o => CancelFromTimeoutRelay( o! );

        static void CancelFromTokenRelay( object o )
        {
            var (c,n) = (Tuple<BaseDeviceCommand,string>)o;
            c.CancelFromCancellationTokens( n );
        }

        static void CancelFromTimeoutRelay( object o )
        {
            ((BaseDeviceCommand)o).CancelFromTimeout();
        }

        readonly CancellationTokenSource _cancelsResult;
        string? _firstCancellationReason;
        int _shouldCallCommandComplete;
        bool _isLocked;
        bool _externalLongRunning;
        // Running data...
        // ...initialized by OnCommandSend.
        internal bool _mustCheckControllerKey;
        IInternalDevice? _device;

        /// <summary>
        /// Initialize a new locked command if <paramref name="locked"/> is provided.
        /// Otherwise initializes a new unlocked command (DeviceName is empty, ControllerKey is null).
        /// </summary>
        /// <param name="locked">The device name and controller key or null.</param>
        private protected BaseDeviceCommand( (string lockedName, string? lockedControllerKey)? locked = null )
        {
            if( locked.HasValue )
            {
                (_deviceName, _controllerKey) = locked.Value;
                _isLocked = true;
            }
            else
            {
                _deviceName = String.Empty;
            }
            _longRunningReason = new TaskCompletionSource<string?>();
            ShouldCallDeviceOnCommandCompleted = true;
            _sendTime = Util.UtcMinValue;
            _cancels = Array.Empty<CancellationTokenRegistration>();
            _cancelsResult = new CancellationTokenSource();
            _cancelsResult.Token.UnsafeRegister( _cancelFromTimeoutHandle, this );
        }

        /// <summary>
        /// Gets the type of the host for the command.
        /// </summary>
        public abstract Type HostType { get; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/> since most of the commands
        /// should not be executed while the device is stopped and this enables always running devices to be resilient to
        /// unattended stops (and subsequent restarts).
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// When <see cref="ImmediateSending"/> is true, <see cref="ImmediateStoppedBehavior"/> applies and this property is ignored.
        /// </para>
        /// </summary>
        protected internal virtual DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.Cancel"/> since most of the commands
        /// should not be executed while the device is stopped.
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceImmediateCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// This applies when <see cref="ImmediateSending"/> is true. See <see cref="StoppedBehavior"/> otherwise.
        /// </para>
        /// </summary>
        protected internal virtual DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.Cancel;

        /// <summary>
        /// Gets or sets whether this command must be sent and handled immediately.
        /// Defaults to false, except for the 5 basic commands (Start, Stop, Configure, SetControllerKey and Destroy).
        /// </summary>
        public bool ImmediateSending
        {
            get => _sendTime.Kind == DateTimeKind.Unspecified;
            set
            {
                // We don't check _isLocked here for 2 reasons:
                // 1 - This is called to configure the already locked basic command default configuration.
                // 2 - this is pointless since Lock() is called after the routing between Immediate and regular
                //     command queues has been made.
                // The side effect is that if Lock() is called by user code before sending the command, this can
                // still be changed. But we don't care :) and more importantly, we use this for Reminder sent
                // as immediate commands (because their due date is in the past).
                _sendTime = value ? DateTime.MinValue : Util.UtcMinValue;
            }
        }

        /// <summary>
        /// Gets or sets the sending time of this command.
        /// When null (the default) or set to <see cref="Util.UtcMinValue"/> the command is executed
        /// (as usual) when it is dequeued.
        /// <para>
        /// When <see cref="ImmediateSending"/> is set to true, this SendingTimeUtc is automatically set to null.
        /// And when this is set to a non null UTC time, the ImmediateSending is automatically set to false.
        /// </para>
        /// <para>
        /// The value should be in the future but no check is done against <see cref="DateTime.UtcNow"/>
        /// in order to safely handle any clock drift: if the time is in the past when the command is dequeued,
        /// it will be executed like any regular (non immediate) command.
        /// </para>
        /// </summary>
        public DateTime? SendingTimeUtc
        {
            get => _sendTime.Ticks == 0 ? null : _sendTime;
            set
            {
                ThrowOnLocked();
                if( !value.HasValue )
                {
                    _sendTime = Util.UtcMinValue;
                }
                else
                {
                    Throw.CheckArgument( value.Value.Kind == DateTimeKind.Utc );
                    _sendTime = value.Value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether <see cref="Device{TConfiguration}.OnCommandCompletedAsync(IActivityMonitor, BaseDeviceCommand)"/>
        /// should be called once the command completed.
        /// Defaults to true, except for the 5 basic commands (Start, Stop, Configure, SetControllerKey and Destroy).
        /// <para>
        /// It is always false once OnCommandCompletedAsync has been called.
        /// </para>
        /// </summary>
        public bool ShouldCallDeviceOnCommandCompleted
        {
            get => _shouldCallCommandComplete == 1;
            set
            {
                if( value ) Interlocked.CompareExchange( ref _shouldCallCommandComplete, 1, 0 );
                else Interlocked.CompareExchange( ref _shouldCallCommandComplete, 0, 1 );
            }
        }

        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/> requires this name to
        /// be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Note that when this command is sent to the device, this name must not be null nor empty (and, more generally,
        /// <see cref="CheckValidity(IActivityMonitor)"/> must return true).
        /// </para>
        /// </summary>
        public string DeviceName
        {
            get => _deviceName;
            set
            {
                Throw.CheckNotNullArgument( value );
                if( value != _deviceName )
                {
                    ThrowOnLocked();
                    _deviceName = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the required controller key. See <see cref="IDevice.ControllerKey"/>
        /// and <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>.
        /// <para>
        /// Note that if the target <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey
        {
            get => _controllerKey;
            set
            {
                ThrowOnLocked();
                _controllerKey = value;
            }
        }

        /// <summary>
        /// Gets whether this command has been submitted and should not be altered anymore.
        /// </summary>
        public bool IsLocked => _isLocked;

        /// <summary>
        /// Checks the validity of this command. <see cref="DeviceName"/> must not be null.
        /// This calls the protected <see cref="DoCheckValidity(IActivityMonitor)"/> that should be overridden to
        /// check specific command parameters constraints.
        /// <para>
        /// This can be called even if <see cref="IsLocked"/> is true.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <returns>Whether this configuration is valid.</returns>
        public bool CheckValidity( IActivityMonitor monitor )
        {
            Debug.Assert( HostType != null, "Thanks to the private protected constructor and the generic of <THost>, a command instance has a host type." );
            if( DeviceName == null )
            {
                monitor.Error( $"Command '{ToString()}': DeviceName must not be null." );
                return false;
            }
            if( _device != null )
            {
                monitor.Error( $"{ToString()} has already been sent to device '{_device.FullName}'. A command can only be sent once." );
                return false;
            }
            return DoCheckValidity( monitor );
        }

        /// <summary>
        /// Sets <see cref="IsLocked"/> to true.
        /// Called once the command is submitted (it has already been successfully validated).
        /// This method can be overridden to prepare the command (like cloning internal data).
        /// <para>
        /// Override should ensure that this method can safely be called multiple times.
        /// </para>
        /// </summary>
        public virtual void Lock()
        {
            _isLocked = true;
            // Note: an Immediate command cannot be deferred but nothing
            // prevents its completion to happen later.
            // This would be an error here to set a null long running reason.
        }

        /// <summary>
        /// Helper method that raises an <see cref="InvalidOperationException"/> if <see cref="IsLocked"/> is true.
        /// </summary>
        protected void ThrowOnLocked()
        {
            if( _isLocked ) Throw.InvalidOperationException( $"Command '{ToString()}' is locked." );
        }

        /// <summary>
        /// Gets the reason that explains why this command is a long running one.
        /// <para>
        /// The reason is null when this command is known to be short running: it has already been handled or
        /// should be handled soon.
        /// </para>
        /// <para>
        /// Long running occurs when:
        /// <list type="bullet">
        ///     <item>
        ///     The command is delayed (see <see cref="SendingTimeUtc"/>).
        ///     </item>
        ///     <item>
        ///     The command is deferred (the device is stopped and the command needs and can wait for a running device - see <see cref="StoppedBehavior"/>).
        ///     </item>
        ///     <item>
        ///     The command has been handled but not completed at the end of its handling: its completion will happen later.
        ///     </item>
        /// </list>
        /// </para>
        /// <para>
        /// The reason can be any string if <see cref="TrySetLongRunningReason(string?)"/> is used.
        /// Internally the 3 constants <see cref="LongRunningDeferredReason"/>, <see cref="LongRunningDelayedReason"/>
        /// and <see cref="LongRunningWaitForCompletionReason"/> are used.
        /// </para>
        /// </summary>
        public Task<string?> LongRunningReason => _longRunningReason.Task;

        /// <summary>
        /// Gets whether this command is known to be long running or not.
        /// This is null until this is known.
        /// </summary>
        [SuppressMessage( "Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Result is called when IsCompleted is true." )]
        [SuppressMessage( "Usage", "VSTHRD104:Offer async methods", Justification = "Result is called when IsCompleted is true." )]
        public bool? IsLongRunning => _longRunningReason.Task.IsCompleted ? _longRunningReason.Task.Result != null : null;

        /// <summary>
        /// Atomically tries to set the <see cref="LongRunningReason"/>.
        /// </summary>
        /// <param name="reason">The reason to set. Must not be the empty string.</param>
        /// <returns>True on success, false if a reason was previously set.</returns>
        public bool TrySetLongRunningReason( string? reason )
        {
            Throw.CheckArgument( reason == null || reason.Length > 0 );
            if( _longRunningReason.TrySetResult( reason ) )
            {
                // Since this can be called concurrently by any piece of code,
                // we cannot call Device.OnLongRunningCommandAppearedAsync here.
                // Instead, we memorize that a long running reason has been provided and
                // we wait for the CommandRunLoopAsync to call its TrySetLongRunningCommandAsync
                // (that calls our DeviceSetLongRunningReason below) either because the command is
                // deferred or delayed or HandleCommandAsync did not complete the command.
                //
                // The side effect is that an externally set long running reason is not
                // immediately tracked. Moreover, if the command has been completed by
                // HandleCommandAsync, the device OnLongRunningCommandAppearedAsync will
                // never see the command.
                //
                // This is not necessarily bad: the command has been marked as long running
                // but it appears that it is not. Unfortunately we don't hide this false positive
                // to the external world (since the LongRunningReason is completed with a non null
                // reason).
                //  
                _externalLongRunning = reason != null;
                return true;
            }
            return false;
        }

        internal bool DeviceSetLongRunningReason( string reason )
        {
            Debug.Assert( reason == LongRunningDeferredReason || reason == LongRunningDelayedReason || reason == LongRunningWaitForCompletionReason );
            return _externalLongRunning || _longRunningReason.TrySetResult( reason );
        }

        /// <summary>
        /// Gets the cancellation reason if a cancellation occurred.
        /// </summary>
        public string? CancellationReason => InternalCompletion.HasBeenCanceled ? _firstCancellationReason : null;

        /// <summary>
        /// Gets the cancellation reason set even if this command is not canceled because of race condition.
        /// <para>
        /// This is set and available before the completion is canceled when a cancellation occurs by <see cref="Cancel(string)"/>,
        /// the timeout set by <see cref="Device{TConfiguration}.GetCommandTimeoutAsync(IActivityMonitor, BaseDeviceCommand)"/>,
        /// or one of the registered token in <see cref="AddCancellationSource(CancellationToken, string)"/>.
        /// </para>
        /// <para>
        /// The only case where it is null and the completion is about to be canceled is when the cancellation occurs on
        /// the <see cref="CompletionSource"/> directly. In this case, the default <see cref="CommandCompletionCanceledReason"/> string
        /// will be set after the command is canceled.
        /// </para>
        /// <para>
        /// This is available to specialized commands so that they can use this information in their optional <see cref="ICompletable.OnCanceled(ref CompletionSource.OnCanceled)"/>
        /// or <see cref="ICompletable{TResult}.OnCanceled(ref CompletionSource{TResult}.OnCanceled)"/> hook.
        /// </para>
        /// </summary>
        /// <returns>The cancellation reason set.</returns>
        protected string? GetFirstCancellationReason() => _firstCancellationReason;

        /// <summary>
        /// Cancels this command with an explicit reason.
        /// </summary>
        /// <param name="reason">
        /// The reason to cancel the command. Must not be empty or whitespace.
        /// This must not be empty or whitespace nor <see cref="CommandCompletionCanceledReason"/>, <see cref="CommandTimeoutReason"/> or <see cref="SendCommandTokenReason"/>.
        /// </param>
        public void Cancel( string reason )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( reason );
            Throw.CheckArgument( reason != CommandTimeoutReason && reason != CommandCompletionCanceledReason && reason != SendCommandTokenReason );
            if( !InternalCompletion.IsCompleted )
            {
                Interlocked.CompareExchange( ref _firstCancellationReason, reason, null );
                _cancelsResult.Cancel();
            }
        }

#pragma warning disable CA1068 // CancellationToken parameters must come last
        /// <summary>
        /// Registers a source for this <see cref="CancellationToken"/> along with a reason.
        /// Nothing is done if <see cref="CancellationToken.CanBeCanceled"/> is false
        /// or this command has already been completed (see <see cref="ICompletion.IsCompleted"/>).
        /// <para>
        /// Whenever one of the added token is canceled, <see cref="ICompletionSource.TrySetCanceled()"/> is called.
        /// If the token is already canceled, the call to try to cancel the completion is made immediately.
        /// </para>
        /// </summary>
        /// <param name="t">The token.</param>
        /// <param name="reason">
        /// Reason that will be <see cref="CancellationReason"/> if this token is the first to cancel the command.
        /// This must not be empty or whitespace nor <see cref="CommandCompletionCanceledReason"/>, <see cref="CommandTimeoutReason"/> or <see cref="SendCommandTokenReason"/>.
        /// </param>
        /// <returns>True if the token has been registered or triggered the cancellation, false otherwise.</returns>
        public bool AddCancellationSource( CancellationToken t, string reason )
        {
            Throw.CheckNotNullOrEmptyArgument( reason );
            Throw.CheckArgument( reason != CommandTimeoutReason && reason != CommandCompletionCanceledReason && reason != SendCommandTokenReason );
            return DoAddCancellationSource( t, reason );
        }

        // Allow internal commands such as WaitForSynchronizationAsync to call this.
        private protected bool DoAddCancellationSource( CancellationToken t, string reason )
        {
            if( t.CanBeCanceled && !InternalCompletion.IsCompleted )
            {
                if( t.IsCancellationRequested )
                {
                    CancelFromCancellationTokens( reason );
                }
                else
                {
                    // Register returns a dummy registration if the token has been signaled.
                    var c = t.UnsafeRegister( _cancelFromTokenHandle, Tuple.Create( this, reason ) );
                    // Instead of using CreateLinkedTokenSource and its linked list
                    // for which we'll have to handle the head with an interlocked setter anyway,
                    // we use a reallocated array of registrations because:
                    //  - it results in much less allocations (one array of 2-tuple instead of the linked nodes).
                    //  - concurrency should be exceptional (interlocked operations will almost always occur once).
                    Util.InterlockedAdd( ref _cancels, c );
                }
                return true;
            }
            return false;
        }
#pragma warning restore CA1068 // CancellationToken parameters must come last

        /// <summary>
        /// Gets a cancellation token that combines all tokens added by <see cref="AddCancellationSource(CancellationToken, string)"/>,
        /// command timeout, and cancellations on the Completion or via <see cref="Cancel(string)"/>.
        /// It must be used to cancel any operation related to the command execution. 
        /// </summary>
        public CancellationToken CancellationToken => _cancelsResult.Token;

        void CancelFromCancellationTokens( string reason )
        {
            Interlocked.CompareExchange( ref _firstCancellationReason, reason, null );
            // This will call CancelFromTimeout() but the _firstCancellationReason
            // will already be set.
            // CancelFromTimeout() then calls InternalCompletion.TrySetCanceled() and
            // this triggers a call (if the cancellation succeeded) to OnInternalCommandCompleted().
            _cancelsResult.Cancel();
        }

        void CancelFromTimeout()
        {
            Interlocked.CompareExchange( ref _firstCancellationReason, CommandTimeoutReason, null );
            InternalCompletion.TrySetCanceled();
        }

        // Called if when Device.GetCommandTimeoutAsync return a non 0 or negative timeout.
        // Can be called by internal commands as well.
        internal void SetCommandTimeout( int ms )
        {
            _cancelsResult.CancelAfter( ms );
        }

        /// <summary>
        /// Because of covariant return type limitation, this property unifies the <see cref="DeviceCommandNoResult.Completion"/>
        /// and <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        internal abstract ICompletionSource InternalCompletion { get; }

        internal bool OnCommandSend( IInternalDevice device, bool checkControllerKey, CancellationToken token )
        {
            // This can be called more than once: SendRoutedCommand can be used to push back an already
            // handled (but delayed for multiple reasons) command.
            if( _device == null )
            {
                Lock();
                _device = device;

            }
            if( InternalCompletion.IsCompleted )
            {
                // This is already completed but we are accepting the command here...
                //
                // We must be sure that OnCommandCompled is called ONCE (if ShouldCallDeviceOnCommandCompleted is true).
                // To prevent a race condition, the trick here (and in OnInternalCommandCompleted) is to use -1
                // as an atomic marker that OnCommandCompleted has already been called.
                // Thanks to this, we don't need to secure the _device assignation: we are sure to see it here
                // so we can safely miss it in OnInternalCommandCompleted.
                //
                // Note that an already completed command doesn't check the controller key.
                //
                // Note: An already completed command is obviously short running and this 
                // should have been set by OnInternalCommandCompleted or will be soon.
                // We don't set the _longRunningReason here.
                //
                if( Interlocked.CompareExchange( ref _shouldCallCommandComplete, -1, 1 ) == 1 ) 
                {
                    _device.OnCommandCompleted( this );
                }
                return false;
            }
            _mustCheckControllerKey = checkControllerKey;
            if( token.CanBeCanceled ) DoAddCancellationSource( token, SendCommandTokenReason );
            return true;
        }

        // This is called by the ICompletable.OnCompleted implementations of DeviceCommandNoResult
        // and DeviceCommandWithResult.
        private protected void OnInternalCommandCompleted()
        {
            Debug.Assert( InternalCompletion.IsCompleted );
            Util.InterlockedSet( ref _cancels, cancels =>
            {
                foreach( var c in cancels ) c.Dispose();
                return Array.Empty<CancellationTokenRegistration>();
            } );
            if( InternalCompletion.HasBeenCanceled )
            {
                // If the completion itself has been canceled (not from this command layer), then
                // the _firstCancellationReason may still be null.
                Interlocked.CompareExchange( ref _firstCancellationReason, CommandCompletionCanceledReason, null );
            }
            // If the long running reason is not yet set, then it's a short one.
            _longRunningReason.TrySetResult( null );

            // A command can be completed before being sent:
            // we don't call OnCommandCompleted is such case.
            if( _device != null && Interlocked.CompareExchange( ref _shouldCallCommandComplete, -1, 1 ) == 1 )
            {
                _device.OnCommandCompleted( this );
            }
        }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }
}
