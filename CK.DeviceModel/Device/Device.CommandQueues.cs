using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    public abstract partial class Device<TConfiguration>
    {
        readonly Channel<BaseDeviceCommand> _commandQueue;
        readonly Channel<BaseDeviceCommand> _commandQueueImmediate;
        readonly Queue<BaseDeviceCommand> _deferredCommands;
        PriorityQueue<BaseDeviceCommand, long>? _delayedQueue;

        // Timers are not accurate enough. When a due time is set,
        // the repeat time is set to 5 ms so that if the initial tick is
        // missed, one of the repeated one will fix it.
        const uint _timerRepeatTime = 5;
        // Used to handle timer change failure. 0 means it should be stopped,
        // otherwise this contains the next timer time.
        // This may be useless (never seen it failed) but this is handled (and logged).
        long _failedChangeTimerTime = -1;
        Timer? _timer;
        // Current command being handled.
        BaseDeviceCommand? _currentlyExecuting;

        int _immediateCommandLimitOffset;
        // Updated by HandleReconfigureAsync (don't trust the actually mutable configuration for security).
        int _baseImmediateCommandLimit;
        volatile bool _immediateCommandLimitDirty;
        // Read & Updated only by the Command loop.
        int _currentImmediateCommandLimit;

        /// <inheritdoc />
        public int ImmediateCommandLimitOffset
        {
            get => _immediateCommandLimitOffset;
            set
            {
                _immediateCommandLimitOffset = value;
                _immediateCommandLimitDirty = true;
            }
        }

        /// <summary>
        /// Dummy command that is used to awake the command loop.
        /// This does nothing and is ignored except that, just like any other commands that are
        /// dequeued, this handles the _commandQueueImmediate execution.
        /// </summary>
        sealed class CommandAwaker : BaseDeviceCommand
        {
            public CommandAwaker() : base( (string.Empty, null) ) { }
            public override Type HostType => throw new NotImplementedException();
            internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
            protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
            public override string ToString() => nameof( CommandAwaker );
        }
        static readonly CommandAwaker _commandAwaker = new();

        #region CommandCanceler

        /// <summary>
        /// Special low level immediate command that can cancel pending commands.
        /// This is an immediate command that doesn't call OnCommandCompleted.
        /// </summary>
        sealed class CommandCanceler : DeviceCommandWithResult<(int, int, int)>
        {
            public readonly bool QueuedCommands;
            public readonly bool DelayedCommands;
            public readonly bool DeferredCommands;

            public CommandCanceler( bool cancelQueuedCommands, bool cancelDelayedCommands, bool cancelDeferredCommands )
                : base( (string.Empty, null) )
            {
                QueuedCommands = cancelQueuedCommands;
                DelayedCommands = cancelDelayedCommands;
                DeferredCommands = cancelDeferredCommands;
                ShouldCallDeviceOnCommandCompleted = false;
            }
            public override Type HostType => throw new NotImplementedException();
            protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
            protected override string ToStringSuffix => $"( Queued:{QueuedCommands}, Delayed:{DelayedCommands}, Deferred:{DelayedCommands} )";
        }

        /// <inheritdoc />
        public Task<(int, int, int)> CancelAllPendingCommandsAsync( bool cancelQueuedCommands, bool cancelDelayedCommands, bool cancelDeferredCommands )
        {
            if( cancelQueuedCommands || cancelDelayedCommands || cancelDeferredCommands )
            {
                var c = new CommandCanceler( cancelQueuedCommands, cancelDelayedCommands, cancelDeferredCommands );
                if( _commandQueueImmediate.Writer.TryWrite( c ) && _commandQueue.Writer.TryWrite( _commandAwaker ) )
                {
                    return c.Completion.Task;
                }
            }
            return Task.FromResult( (0, 0, 0) );
        }

        void HandleCancelAllPendingCommands( CommandCanceler cmd )
        {
            using( _commandMonitor.OpenInfo( $"Executing {cmd}." ) )
            {
                int cRemoved = 0;
                if( cmd.QueuedCommands )
                {
                    while( _commandQueue.Reader.TryRead( out var c ) )
                    {
                        Debug.Assert( c.IsLocked );
                        if( c != _commandAwaker )
                        {
                            c.InternalCompletion.SetCanceled();
                            ++cRemoved;
                        }
                    }
                    // Security: run the loop once done.
                    _commandQueue.Writer.TryWrite( _commandAwaker );
                    _commandMonitor.Info( $"Canceled {cRemoved} waiting commands." );
                }
                int tRemoved = 0;
                if( _delayedQueue != null )
                {
                    tRemoved = _delayedQueue.Count;
                    foreach( var (t, _) in _delayedQueue.UnorderedItems )
                    {
                        if( t is not Reminder )
                        {
                            t.InternalCompletion.SetCanceled();
                        }
                    }
                    _delayedQueue.Clear();
                }
                int dRemoved = _deferredCommands.Count;
                if( cmd.DeferredCommands )
                {
                    _commandMonitor.Info( $"Canceled {dRemoved} deferred commands." );
                    while( _deferredCommands.TryDequeue( out var c ) )
                    {
                        c.InternalCompletion.SetCanceled();
                    }
                }
                cmd.Completion.SetResult( (cRemoved, tRemoved, dRemoved) );
            }
        }

        #endregion

        #region SendCommand methods
        /// <inheritdoc />
        public bool SendCommand( IActivityMonitor monitor,
                                 BaseDeviceCommand command,
                                 bool checkDeviceName = true,
                                 bool checkControllerKey = true,
                                 CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName );
            return command.ImmediateSending
                            ? SendRoutedCommandImmediate( command, checkControllerKey, token )
                            : SendRoutedCommand( command, checkControllerKey, token );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommand( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false );
            monitor.Trace( IDeviceHost.DeviceModel, $"Sending {(command.ImmediateSending ? "immediate" : "")} '{command}' to device '{Name}'." );
            return command.ImmediateSending
                            ? SendRoutedCommandImmediate( command, false, token )
                            : SendRoutedCommand( command, false, token );
        }

        /// <summary>
        /// Sends the given command directly in the waiting queue.
        /// This is to be used for low level internal commands, typically initiated by timers.
        /// </summary>
        /// <param name="command">The command to send without any checks.</param>
        /// <param name="checkControllerKey">Optionally checks the ControllerKey.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        internal protected bool SendRoutedCommand( BaseDeviceCommand command, bool checkControllerKey = false, CancellationToken token = default )
        {
            command.OnCommandSend( this, checkControllerKey, token );
            return _commandQueue.Writer.TryWrite( command );
        }

        /// <summary>
        /// Sends the given command directly for immediate execution.
        /// This is to be used for low level internal commands, typically initiated by timers.
        /// </summary>
        /// <param name="command">The command to send without any checks.</param>
        /// <param name="checkControllerKey">Optionally checks the ControllerKey.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        internal protected bool SendRoutedCommandImmediate( BaseDeviceCommand command, bool checkControllerKey = false, CancellationToken token = default )
        {
            command.OnCommandSend( this, checkControllerKey, token );
            return _commandQueueImmediate.Writer.TryWrite( command )
                   && _commandQueue.Writer.TryWrite( _commandAwaker );
        }

        void CheckDirectCommandParameter( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( command );
            if( !command.HostType.IsAssignableFrom( _host!.GetType() ) ) Throw.ArgumentException( nameof( command ), $"{command}: Invalid HostType '{command.HostType.Name}'." );
            if( !command.CheckValidity( monitor ) ) Throw.ArgumentException( nameof( command ), $"{command}: CheckValidity failed. See logs." );
            if( checkDeviceName )
            {
                if( command.DeviceName != Name )
                {
                    Throw.ArgumentException( nameof( command ), $"{command}: Command DeviceName is '{command.DeviceName}', device '{Name}' cannot execute it. (For direct execution, you can use checkDeviceName: false parameter to skip this check or use UnsafeSendCommand.)" );
                }
            }
        } 
        #endregion

        async Task CommandRunLoopAsync()
        {
            bool wasStop = true;
            UpdateImmediateCommandLimit();

            // Regular command.
            BaseDeviceCommand? cmd = null;

            // Immediate command.
            BaseDeviceCommand? immediate;

            while( !IsDestroyed )
            {
                cmd = _currentlyExecuting = null;
                try
                {
                    // Before waiting for the next regular command or _commandAwaker, tries to retrieve
                    // a delayed command that must be executed.
                    // Ready-to-run delayed commands are picked one by one and handled as if they were coming from the
                    // regular queue. Immediate commands are always handled first.

                    // Dequeuing a delayed command also returns the fact that a next one is ready to run.
                    // Once immediate are handled, we avoid the flush of the awakers and the lookup from the regular queue
                    // (if the current command is the awaker): this de facto enables ready-to-run delayed commands to be handled
                    // at a higher priority than regular ones.
                    bool delayedCommandReady = false;
                    if( _delayedQueue != null ) (cmd, delayedCommandReady) = TryGetDelayedCommand();
                    if( cmd == null )
                    {
                        _commandMonitor.Debug( "Waiting for command..." );
                        cmd = await _commandQueue.Reader.ReadAsync().ConfigureAwait( false );
                    }
                    _commandMonitor.Debug( $"Obtained {cmd}." );

                    #region Immediate commands
                    // Handle all available immediate at once (but no more than ImmediateCommandLimit to avoid regular commands starvation).
                    if( _commandQueueImmediate.Reader.TryRead( out immediate ) )
                    {
                        Debug.Assert( immediate != null );
                        await HandleImmediateCommandsAsync( immediate ).ConfigureAwait( false );
                    }
                    #endregion

                    #region Handles low-level ReminderCommand (from the _delayedQueue).
                    if( cmd is Reminder reminder )
                    {
                        try
                        {
                            await OnReminderAsync( _commandMonitor, reminder.Time, reminder.State ).ConfigureAwait( false );
                        }
                        catch( Exception ex )
                        {
                            using( _commandMonitor.OpenFatal( $"Unhandled error in OnReminderAsync. Stopping the device '{FullName}'.", ex ) )
                            {
                                Debug.Assert( _host != null );
                                await HandleStopAsync( null, DeviceStoppedReason.SelfStoppedForceCall );
                            }
                        }
                        continue;
                    }
                    #endregion

                    #region If device is starting, process Deferred commands (interleaved with immediate)
                    if( wasStop && IsRunning )
                    {
                        // Executing deferred commands is a subordinated loop that alternates deferred and immediate commands: both loops are
                        // constrained by their respective limits.
                        if( _deferredCommands.Count > 0 )
                        {
                            Debug.Assert( !IsDestroyed );
                            using( _commandMonitor.OpenInfo( $"Device started: executing {_deferredCommands.Count} deferred commands." ) )
                            {
                                while( IsRunning && _deferredCommands.TryDequeue( out var deferred ) )
                                {
                                    await HandleCommandAsync( deferred, allowDefer: false, isImmediate: false ).ConfigureAwait( false );
                                    if( !IsDestroyed && _commandQueueImmediate.Reader.TryRead( out immediate ) )
                                    {
                                        await HandleImmediateCommandsAsync( immediate ).ConfigureAwait( false );
                                    }
                                }
                            }
                        }
                    }
                    #endregion

                    if( IsDestroyed )
                    {
                        if( cmd != _commandAwaker )
                        {
                            _commandMonitor.Trace( $"Setting UnavailableDeviceException on {cmd} (about to be handled)." );
                            cmd.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd ) );
                        }
                        // Breaks the loop: let the queues be flushed below.
                        break;
                    }

                    // Before flushing any command awaker to get an actual command,
                    // if a delayed command is ready to run, give it the priority.
                    if( cmd == _commandAwaker && delayedCommandReady ) continue;

                    // Updates the wasStop flag.
                    wasStop = !IsRunning;

                    while( cmd == _commandAwaker )
                    {
                        if( !_commandQueue.Reader.TryRead( out var oneRegular ) ) break;
                        cmd = oneRegular;
                        if( cmd == _commandAwaker ) _commandMonitor.Debug( "CommandAwaker flushed." );
                    }
                    if( cmd == _commandAwaker )
                    {
                        // No regular command found. Before awaiting the next command,
                        // handle any potential immediate ones since we may have dequeued the last
                        // awaker.
                        if( _commandQueueImmediate.Reader.TryRead( out immediate ) )
                        {
                            await HandleImmediateCommandsAsync( immediate ).ConfigureAwait( false );
                        }
                        continue;
                    }
                    Debug.Assert( cmd.IsLocked );
                    // Now that immediate and potential deferred have been handled, consider the SendingTimeUtc.
                    if( EnqueueDelayed( cmd ) )
                    {
                        _commandMonitor.Trace( $"Delaying '{cmd}', SendingTimeUtc: {cmd.SendingTimeUtc:O}." );
                        continue;
                    }
                    await HandleCommandAsync( cmd, allowDefer: true, isImmediate: false ).ConfigureAwait( false );
                }
                catch( Exception ex )
                {
                    Debug.Assert( _currentlyExecuting != null );
                    using( _commandMonitor.OpenError( $"Unhandled error in '{FullName}' while processing '{_currentlyExecuting}'.", ex ) )
                    {
                        // Always complete the command.
                        if( !_currentlyExecuting.InternalCompletion.TrySetException( ex ) )
                        {
                            _commandMonitor.Warn( $"Command has already been completed. Unable to set the error." );
                        }
                        bool mustStop = true;
                        try
                        {
                            mustStop = await OnUnhandledExceptionAsync( _commandMonitor, _currentlyExecuting, ex ).ConfigureAwait( false );
                        }
                        catch( Exception ex2 )
                        {
                            _commandMonitor.Fatal( $"Device '{FullName}' OnCommandErrorAsync raised an error. Device will stop.", ex2 );
                        }
                        if( mustStop )
                        {
                            if( IsRunning )
                            {
                                _commandMonitor.Warn( $"Sending a stop command to Device '{FullName}'." );
                                Debug.Assert( _host != null );
                                // Fires and forget the StopCommand: the fact that the device stops
                                // does not belong to the faulty command plan.
                                SendRoutedCommandImmediate( _host.CreateStopCommand( Name, ignoreAlwaysRunning: true ) );
                            }
                        }
                    }
                }
            }
            if( _timer != null )
            {
                await _timer.DisposeAsync();
            }
            _commandQueue.Writer.Complete();
            _commandQueueImmediate.Writer.Complete();
            using( _commandMonitor.OpenInfo( $"Ending device loop, flushing command queues by signaling a UnavailableDeviceException." ) )
            {
                while( _commandQueueImmediate.Reader.TryRead( out immediate ) )
                {
                    _commandMonitor.Trace( $"Setting UnavailableDeviceException on '{immediate}' (from immediate queue)." );
                    immediate.InternalCompletion.TrySetException( new UnavailableDeviceException( this, immediate ) );
                }
                while( _deferredCommands.TryDequeue( out cmd ) )
                {
                    _commandMonitor.Trace( $"Setting UnavailableDeviceException on {cmd} (from deferred queue)." );
                    cmd.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd ) );
                }
                while( _commandQueue.Reader.TryRead( out cmd ) )
                {
                    if( cmd != _commandAwaker )
                    {
                        _commandMonitor.Trace( $"Setting UnavailableDeviceException on {cmd} (from command queue)." );
                        cmd.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd ) );
                    }
                }
                if( _delayedQueue != null )
                {
                    foreach( var (d, _) in _delayedQueue.UnorderedItems )
                    {
                        _commandMonitor.Trace( $"Setting UnavailableDeviceException on {d} (from delayed command queue)." );
                        d.InternalCompletion.TrySetException( new UnavailableDeviceException( this, d ) );
                    }
                }
            }
            _commandMonitor.MonitorEnd();
        }

        bool EnqueueDelayed( BaseDeviceCommand cmd )
        {
            Debug.Assert( cmd._sendTime.Kind == DateTimeKind.Utc, "Immediate are not handled here." );
            long time = cmd._sendTime.Ticks / TimeSpan.TicksPerMillisecond;
            long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if( time > 0 )
            {
                long lDelta = time - now;
                if( lDelta >= uint.MaxValue )
                {
                    _commandMonitor.Error( $"Command '{cmd}' has a totally stupid SendigTimeUtc in the future ({cmd._sendTime:O}). Its is ignored." );
                    return false;
                }
                var delta = (uint)lDelta;
                // If the command must be send in less than 2ms, handle it now.
                if( delta > 1 )
                {
                    if( _delayedQueue == null )
                    {
                        _delayedQueue = new PriorityQueue<BaseDeviceCommand, long>();
                        _timer = new Timer( _ => _commandQueue.Writer.TryWrite( _commandAwaker ) );
                        _commandMonitor.Info( "Created DelayedQueue and Timer." );
                    }
                    _delayedQueue.Enqueue( cmd, time );
                    if( _delayedQueue.Peek() == cmd )
                    {
                        Debug.Assert( _timer != null );
                        _commandMonitor.Debug( $"First delayed command in {delta} ms." );
                        if( !_timer.Change( delta, _timerRepeatTime ) )
                        {
                            _commandMonitor.Debug( $"Failed to Change timer." );
                            _failedChangeTimerTime = time;
                            _commandQueue.Writer.TryWrite( _commandAwaker );
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        (BaseDeviceCommand?,bool) TryGetDelayedCommand()
        {
            Debug.Assert( _delayedQueue != null );
            Debug.Assert( _timer != null );

            BaseDeviceCommand? cmd = null;
            var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            // First tries to Change the timer if a previous attempt failed.
            if( _failedChangeTimerTime != -1 ) RetryTimerConfiguration( now );

            // Next, dequeue the next ready-to-run delayed commands, skipping the canceled ones,
            // with a 1 ms margin: we consider that a 1 ms delay is negligible.
            bool hasDequeued = false;

            BaseDeviceCommand? delayed;
            long nextDelayedTime;
            while( _delayedQueue.TryPeek( out delayed, out nextDelayedTime )
                   && nextDelayedTime <= now + 1 )
            {
                hasDequeued = true;
                _delayedQueue.Dequeue();
                if( delayed.CancellationToken.IsCancellationRequested )
                {
                    _commandMonitor.Debug( $"Skipped canceled Delayed Command {delayed}." );
                    continue;
                }
                _commandMonitor.Debug( $"Delayed Command dequeued: {delayed} {delayed._sendTime:O}." );
                // The command is no more delayed.
                delayed._sendTime = Util.UtcMinValue;
                cmd = delayed;
                break;
            }
            bool nextIsReady = false;
            if( hasDequeued )
            {
                if( _delayedQueue.TryPeek( out delayed, out nextDelayedTime ) )
                {
                    var delta = nextDelayedTime - now;
                    // If a delayed command is waiting, we activate the timer that will send a _commandAwaker.
                    // We use the same 1 ms margin as above.
                    if( delta > 1 )
                    {
                        Debug.Assert( _delayedQueue.Count > 0 );
                        _commandMonitor.Debug( $"Next delayed command is {delayed} in {delta} ms." );
                        _failedChangeTimerTime = -1;
                        if( !_timer.Change( (uint)delta, _timerRepeatTime ) )
                        {
                            _commandMonitor.Error( $"Failed to Change timer. Sending CommandAwaker to loop." );
                            _failedChangeTimerTime = nextDelayedTime;
                            _commandQueue.Writer.TryWrite( _commandAwaker );
                        }
                    }
                    else
                    {
                        _commandMonitor.Debug( "Next delayed command is ready to be dequeued. Stopping timer." );
                        StopTimer();
                        nextIsReady = true;
                    }
                }
                else
                {
                    _commandMonitor.Debug( "No more delayed command. Stopping timer." );
                    StopTimer();
                }
            }
            else _commandMonitor.Debug( "No ready-to-run delayed command." );
            return (cmd,nextIsReady);
        }

        void StopTimer()
        {
            Debug.Assert( _timer != null );
            _failedChangeTimerTime = -1;
            if( !_timer.Change( Timeout.Infinite, Timeout.Infinite ) )
            {
                _commandMonitor.Error( "Failed to Stop timer. Sending CommandAwaker to loop." );
                _failedChangeTimerTime = 0;
                _commandQueue.Writer.TryWrite( _commandAwaker );
            }
        }

        private void RetryTimerConfiguration( long now )
        {
            using( _commandMonitor.OpenWarn( $"Retrying to Change timer." ) )
            {
                var delta = _failedChangeTimerTime - now;
                if( delta > 0 )
                {
                    if( !_timer.Change( (uint)delta, _timerRepeatTime ) )
                    {
                        _commandMonitor.Error( $"Failed to Change timer (again) to {delta} ms. Sending CommandAwaker to loop." );
                        _commandQueue.Writer.TryWrite( _commandAwaker );
                    }
                    else
                    {
                        _commandMonitor.Info( $"Timer Change succeeded: Set to {delta} ms." );
                        _failedChangeTimerTime = -1;
                    }
                }
                else
                {
                    if( _failedChangeTimerTime != 0 ) _commandMonitor.Error( "Required timer expired. Give up. Stopping it." );
                    bool done = _timer.Change( Timeout.Infinite, Timeout.Infinite );
                    if( !done )
                    {
                        _commandMonitor.Error( "Stopping timer failed." );
                        _failedChangeTimerTime = 0;
                    }
                    else
                    {
                        _commandMonitor.Info( "Timer stopped." );
                        _failedChangeTimerTime = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Handles ImmediateCommandLimit immediate commands:
        /// <list type="bullet">
        ///  <item>
        ///         CommandCanceler calls HandleCancelAllPendingCommands.
        ///  </item>
        ///  <item>
        ///     If the command is completed (it has been added to the immediate queue by IInternalDeviceHost.OnCommandCompleted),
        ///     virtual OnCommandCompletedAsync() is called and if an unhandled exception occurs, the device is force stopped.
        ///     These completions don't count for ImmediateCommandLimit.
        ///  </item>
        ///  <item>
        ///     Any other immediate commands are handled by HandleCommandAsync (with no possible defer).
        ///  </item>
        /// </list>
        /// </summary>
        /// <param name="immediate">The first immediate command.</param>
        /// <returns>The awaitable.</returns>
        async Task HandleImmediateCommandsAsync( BaseDeviceCommand immediate )
        {
            if( _immediateCommandLimitDirty ) UpdateImmediateCommandLimit();
            int maxCount = _currentImmediateCommandLimit;
            do
            {
                if( immediate is CommandCanceler c )
                {
                    HandleCancelAllPendingCommands( c );
                }
                else if( immediate.InternalCompletion.IsCompleted )
                {
                    // Command completion don't count as immediate commands.
                    ++maxCount;
                    try
                    {
                        await OnCommandCompletedAsync( _commandMonitor, immediate );
                    }
                    catch( Exception ex )
                    {
                        using( _commandMonitor.OpenFatal( $"Unhandled error in OnCommandCompletedAsync for '{immediate}'. Stopping the device '{FullName}'.", ex ) )
                        {
                            Debug.Assert( _host != null );
                            await HandleStopAsync( null, DeviceStoppedReason.SelfStoppedForceCall );
                        }
                    }
                }
                else
                {
                    await HandleCommandAsync( immediate, allowDefer: false, isImmediate: true ).ConfigureAwait( false );
                }
            }
            while( !IsDestroyed && --maxCount > 0 && _commandQueueImmediate.Reader.TryRead( out immediate! ) );
            // It would not be a good idea to log here that maxCount reached 0 since it could be a false positive and that
            // we cannot inspect whether another immediate is waiting in the queue.
            // Even when the "queue tracking" will be implemented (via Interlocked.Inc/Decrement since channels have no way to tell us how many items
            // they have), logging here would not be really interesting. It's better to find a way to collect and represents the metrics and let
            // the limit here do its job silently: the metrics itself must show the status' of the queues.
        }

        void UpdateImmediateCommandLimit()
        {
            _immediateCommandLimitDirty = false;
            int offset = _immediateCommandLimitOffset;
            int l = _baseImmediateCommandLimit + offset;
            if( l <= 0 )
            {
                l = 1;
                _commandMonitor.Warn( $"Out of range ImmediateCommandLimitOffset ({offset}) since Configuration.BaseImmediateCommandLimitOffset is {_baseImmediateCommandLimit}. The sum must be positive." );
            }
            else if( l > 1000 )
            {
                l = 1000;
                _commandMonitor.Warn( $"Out of range ImmediateCommandLimitOffset ({offset}) since Configuration.BaseImmediateCommandLimitOffset is {_baseImmediateCommandLimit}. The sum must not be greater than 1000." );
            }
            if( _currentImmediateCommandLimit != l )
            {
                _currentImmediateCommandLimit = l;
                _commandMonitor.Info( $"ImmediateCommandLimit set to {l}." );
            }
        }

        // Called by HandleCommandAsync (below) when the device is stopped and DeviceCommandStoppedBehavior
        // is AutoStartAndKeepRunning or SilentAutoStartAndStop.
        async Task HandleCommandAutoStartAsync( BaseDeviceCommand command, bool withStop )
        {
            using var g = _commandMonitor.OpenDebug( $"Starting device for command '{command}' handling and {(withStop ? "stopping it after" : "let it run")}." );
            Debug.Assert( !IsRunning );
            await HandleStartAsync( null, withStop ? DeviceStartedReason.SilentAutoStartAndStopStoppedBehavior : DeviceStartedReason.StartAndKeepRunningStoppedBehavior ).ConfigureAwait( false );
            if( IsRunning )
            {
                try
                {
                    int t = await GetCommandTimeoutAsync( _commandMonitor, command ).ConfigureAwait( false );
                    if( t > 0 ) command.SetCommandTimeout( t );
                    if( !command.CancellationToken.IsCancellationRequested )
                    {
                        await DoHandleCommandAsync( _commandMonitor, command ).ConfigureAwait( false );
                    }
                }
                finally
                {
                    if( withStop )
                    {
                        if( IsRunning )
                        {
                            await HandleStopAsync( null, DeviceStoppedReason.SilentAutoStartAndStopStoppedBehavior ).ConfigureAwait( false );
                        }
                        else
                        {
                            _commandMonitor.Debug( "Device has already been stopped." );
                        }
                    }
                    else
                    {
                        if( IsRunning )
                        {
                            // Awake the queue for deferred commands.
                            _commandQueue.Writer.TryWrite( _commandAwaker );
                        }
                    }
                }
            }
            else
            {
                _commandMonitor.Warn( $"Failed to start the device. Canceling the AutoStarting command '{command}'." );
                command.Cancel( "DeviceAutoStartFailed" );
            }
        }

        async Task HandleCommandAsync( BaseDeviceCommand command, bool allowDefer, bool isImmediate )
        {
            // No catch here: let the exception be handled by the main catch in HandleCommandAsyc that relies on _currentlyExecuting.
            _currentlyExecuting = command;
            using var g = _commandMonitor.OpenTrace( $"Handling command '{command}'." )
                                         .ConcludeWith( () => command.ToString()! );

            if( command.CancellationToken.IsCancellationRequested ) return;
            // Basic commands are all by design AlwaysRunning: no call to OnStoppedBehavior must be made,
            // but controller key must be checked for each of them.
            // The check is repeated instead of duplicating the switch.
            switch( command )
            {
                case BaseStopDeviceCommand stop:
                    if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                    await HandleStopAsync( stop, DeviceStoppedReason.StoppedCall ).ConfigureAwait( false );
                    return;
                case BaseStartDeviceCommand start:
                    if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                    await HandleStartAsync( start, DeviceStartedReason.StartCall ).ConfigureAwait( false );
                    return;
                case BaseConfigureDeviceCommand<TConfiguration> config:
                    if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                    await HandleReconfigureAsync( config ).ConfigureAwait( false );
                    return;
                case BaseSetControllerKeyDeviceCommand setC:
                    if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                    await HandleSetControllerKeyAsync( setC ).ConfigureAwait( false );
                    return;
                case BaseDestroyDeviceCommand destroy:
                    if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                    await HandleDestroyAsync( destroy, false ).ConfigureAwait( false );
                    return;
                default:
                    {
                        if( !IsRunning )
                        {
                            if( isImmediate )
                            {
                                using( _commandMonitor.OpenInfo( $"Handling immediate command '{command}' while device is stopped with Command.ImmediateStoppedBehavior = '{command.ImmediateStoppedBehavior}'." ) )
                                {
                                    var behavior = OnStoppedDeviceImmediateCommand( _commandMonitor, command );
                                    if( behavior != command.ImmediateStoppedBehavior )
                                    {
                                        _commandMonitor.Debug( $"OnStoppedDeviceImmediateCommand returned ImmediateStoppedBehavior = '{behavior}'" );
                                    }
                                    switch( behavior )
                                    {
                                        case DeviceImmediateCommandStoppedBehavior.SetUnavailableDeviceException:
                                            _commandMonitor.CloseGroup( $"Setting UnavailableDeviceException on command." );
                                            command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                            return;
                                        case DeviceImmediateCommandStoppedBehavior.Cancel:
                                            _commandMonitor.CloseGroup( $"Canceling command." );
                                            command.Cancel( nameof( DeviceImmediateCommandStoppedBehavior ) );
                                            return;
                                        case DeviceImmediateCommandStoppedBehavior.RunAnyway:
                                            _commandMonitor.CloseGroup( $"Let the command be handled anyway." );
                                            break;
                                        default: throw new NotSupportedException( "Unknown DeviceImmediateCommandStoppedBehavior." );
                                    }
                                }
                            }
                            else
                            {
                                using( _commandMonitor.OpenInfo( $"Handling command '{command}' while device is stopped with Command.StoppedBehavior = '{command.StoppedBehavior}'." ) )
                                {
                                    var behavior = OnStoppedDeviceCommand( _commandMonitor, command );
                                    if( behavior != command.StoppedBehavior )
                                    {
                                        _commandMonitor.Debug( $"OnStoppedDeviceCommand returned StoppedBehavior = '{behavior}'" );
                                    }
                                    switch( behavior )
                                    {
                                        case DeviceCommandStoppedBehavior.AutoStartAndKeepRunning:
                                        case DeviceCommandStoppedBehavior.SilentAutoStartAndStop:
                                            if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                                            await HandleCommandAutoStartAsync( command, withStop: command.StoppedBehavior == DeviceCommandStoppedBehavior.SilentAutoStartAndStop );
                                            return;
                                        case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel:
                                            if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                                            {
                                                PushDeferredCommand( command );
                                            }
                                            else
                                            {
                                                _commandMonitor.CloseGroup( "Canceling command." );
                                                command.Cancel( "DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel" );
                                            }
                                            return;
                                        case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrSetUnavailableDeviceException:
                                            if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                                            {
                                                PushDeferredCommand( command );
                                            }
                                            else
                                            {
                                                _commandMonitor.CloseGroup( "Setting UnavailableDeviceException on command." );
                                                command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                            }
                                            return;
                                        case DeviceCommandStoppedBehavior.SetUnavailableDeviceException:
                                            _commandMonitor.CloseGroup( "Setting UnavailableDeviceException on command." );
                                            command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                            return;
                                        case DeviceCommandStoppedBehavior.Cancel:
                                            _commandMonitor.CloseGroup( "Canceling command." );
                                            command.Cancel( "DeviceCommandStoppedBehavior.Cancel" );
                                            return;
                                        case DeviceCommandStoppedBehavior.AlwaysWaitForNextStart:
                                            if( allowDefer )
                                            {
                                                _commandMonitor.CloseGroup( $"Pushing command to the deferred command queue." );
                                                _deferredCommands.Enqueue( command );
                                            }
                                            else
                                            {
                                                _commandMonitor.CloseGroup( $"Setting UnavailableDeviceException on command." );
                                                command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                            }
                                            return;
                                        case DeviceCommandStoppedBehavior.RunAnyway:
                                            _commandMonitor.CloseGroup( "Let the command be handled anyway." );
                                            break;
                                        default: throw new NotSupportedException( "Unknown DeviceCommandStoppedBehavior." );
                                    }
                                }
                            }
                        }
                        if( command._mustCheckControllerKey && !CheckControllerKey( command ) ) return;
                        int t = await GetCommandTimeoutAsync( _commandMonitor, command ).ConfigureAwait( false );
                        if( t > 0 )
                        {
                            _commandMonitor.Debug( $"Command timeout set to {t} ms." );
                            command.SetCommandTimeout( t );
                        }
                        else
                        {
                            _commandMonitor.Debug( $"No timeout set on command." );
                        }
                        if( !command.CancellationToken.IsCancellationRequested )
                        {
                            await DoHandleCommandAsync( _commandMonitor, command ).ConfigureAwait( false );
                        }
                        break;
                    }
            }
        }

        void PushDeferredCommand( BaseDeviceCommand command )
        {
            // TODO: (internal protected virtual) command.OnDeferring( FIFOBuffer<BaseDeviceCommand> queue ) => queue.Push( this );
            _commandMonitor.CloseGroup( "Pushing command to the deferred command queue." );
            _deferredCommands.Enqueue( command );
        }

        bool CheckControllerKey( BaseDeviceCommand command )
        {
            var key = ControllerKey;
            if( key != null && command.ControllerKey != key )
            {
                var msg = $"{command}: Expected command ControllerKey is '{command.ControllerKey}' but current device's one is '{key}'. (You can use checkControllerKey: false parameter to skip this check or use UnsafeSendCommand.)";
                _commandMonitor.Error( msg );
                command.InternalCompletion.TrySetException( new InvalidControllerKeyException( msg ) );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Extension point that is called for each command that must be executed while this device is stopped.
        /// This default implementation simply returns the <see cref="BaseDeviceCommand.StoppedBehavior"/>.
        /// <para>
        /// This is not called for the basic commands (Start, Stop, Configure, SetControllerKey and Destroy) that must run anyway by design.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command that should be executed, deferred, canceled or set on error.</param>
        /// <returns>The behavior to apply for the command.</returns>
        protected virtual DeviceCommandStoppedBehavior OnStoppedDeviceCommand( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            return command.StoppedBehavior;
        }

        /// <summary>
        /// Extension point that is called for each immediate command that must be executed while this device is stopped.
        /// This default implementation simply returns the <see cref="BaseDeviceCommand.ImmediateStoppedBehavior"/>.
        /// <para>
        /// This is not called for the basic commands (Start, Stop, Configure, SetControllerKey and Destroy) that must run anyway by design.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command that should be executed, canceled or set on error.</param>
        /// <returns>The behavior to apply for the command.</returns>
        protected virtual DeviceImmediateCommandStoppedBehavior OnStoppedDeviceImmediateCommand( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            return command.ImmediateStoppedBehavior;
        }

        void IInternalDevice.OnCommandCompleted( BaseDeviceCommand cmd )
        {
            Debug.Assert( cmd.InternalCompletion.IsCompleted );
            if( _commandQueueImmediate.Writer.TryWrite( cmd ) ) _commandQueue.Writer.TryWrite( _commandAwaker );
        }

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
            }
            public override Type HostType => throw new NotImplementedException();
            internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
            protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
            public override string ToString() => nameof( Reminder );
        }

        /// <summary>
        /// Registers a reminder that must be in the future or, by default, an <see cref="ArgumentException"/> is thrown.
        /// This can be used indifferently on a stopped or running device: <see cref="OnReminderAsync"/> will always
        /// eventually be called.
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
        /// eventually be called.
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

        /// <summary>
        /// Extension point that is called when <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/> raises an exception.
        /// It can be overridden to avoid calling <see cref="IDevice.StopAsync(IActivityMonitor, bool)"/> (that ignores
        /// the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration), when the handling of a command raised an exception,
        /// and/or to emit an event (typically an UnexpectedErrorEvent).
        /// <para>
        /// The faulty <paramref name="command"/> is already completed (at least with the exception): this cannot be undone.
        /// </para>
        /// <para>
        /// This default implementation returns always true: the device is stopped by default.
        /// </para>
        /// <para>
        /// Specialized implementations can call <see cref="IDevice.StopAsync(IActivityMonitor, bool)"/> (or even
        /// <see cref="IDevice.DestroyAsync(IActivityMonitor, bool)"/>) directly if needed.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The culprit command.</param>
        /// <param name="ex">The exception raised.</param>
        /// <returns>True to stop this device, false to let this device run.</returns>
        protected virtual ValueTask<bool> OnUnhandledExceptionAsync( IActivityMonitor monitor, BaseDeviceCommand command, Exception ex ) => new ValueTask<bool>( true );

        /// <summary>
        /// Called right before <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/> to compute a
        /// timeout in milliseconds for the command that is about to be executed.
        /// By default this returns 0: negative or 0 means that no timeout will be set on the command.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command about to be handled by <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/>.</param>
        /// <returns>A timeout in milliseconds. 0 or negative if timeout cannot or shouldn't be set.</returns>
        protected virtual ValueTask<int> GetCommandTimeoutAsync( IActivityMonitor monitor, BaseDeviceCommand command ) => new ValueTask<int>( 0 ); 

        /// <summary>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="NotSupportedException"/>.
        /// <para>
        /// Basic checks have been done on the <paramref name="command"/> object:
        /// <list type="bullet">
        /// <item><see cref="BaseDeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/> (or Device.UnsafeSendCommand or
        /// Device.UnsafeSendCommandImmediate has been used, then the device's name has automatically been set).
        /// </item>
        /// <item>
        /// The <see cref="BaseDeviceCommand.ControllerKey"/> is either null or match the current <see cref="ControllerKey"/>
        /// (or an Unsafe send has been used).
        /// </item>
        /// <item><see cref="BaseDeviceCommand.StoppedBehavior"/> is coherent with the current <see cref="IsRunning"/> state.</item>
        /// </list>
        /// </para>
        /// <para>
        /// This method MUST ensure that the <see cref="DeviceCommandNoResult.Completion"/> or <see cref="DeviceCommandWithResult{TResult}.Completion"/>
        /// is eventually resolved otherwise the caller may indefinitely wait for the command completion.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            // By returning a faulty task here, we'll enter the catch clause of the command execution 
            // and the command's TCS will be set with the exception.
            return Task.FromException( new NotSupportedException( $"Unhandled command type: '{command.GetType().FullName}'." ) );
        }

        /// <summary>
        /// Called when a command completes and its <see cref="BaseDeviceCommand.ShouldCallDeviceOnCommandCompleted"/> is true.
        /// <para>
        /// This default implementation does nothing (returns <see cref="Task.CompletedTask"/>.
        /// </para>
        /// <para>
        /// If an exception is thrown by this method, it is logged and the device is stopped: there is no callback like for
        /// <see cref="DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/>/<see cref="OnUnhandledExceptionAsync(IActivityMonitor, BaseDeviceCommand, Exception)"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The completed command.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command ) => Task.CompletedTask;

    }
}
