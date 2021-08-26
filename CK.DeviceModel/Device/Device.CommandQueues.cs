using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{

    public abstract partial class Device<TConfiguration>
    {
        readonly Channel<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)> _commandQueue;
        Queue<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)> _deferredCommands;
        readonly Channel<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)> _commandQueueImmediate;

        /// <summary>
        /// Dummy command that is used to awake the command loop.
        /// This does nothing and is ignored except that, just like any other commands that are
        /// dequeued, this handles the _commandQueueImmediate execution.
        /// </summary>
        class CommandAwaker : BaseDeviceCommand
        {
            public override Type HostType => throw new NotImplementedException();
            internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
            protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
        }
        static readonly CommandAwaker _commandAwaker = new();


        /// <inheritdoc />
        public (int, int) CancelAllPendingCommands( IActivityMonitor monitor, bool cancelQueuedCommands, bool cancelDeferredCommands )
        {
            int cRemoved = 0;
            if( cancelQueuedCommands )
            {
                while( _commandQueue.Reader.TryRead( out var c ) )
                {
                    c.Command.InternalCompletion.SetCanceled();
                    ++cRemoved;
                }
                // Security: run the loop.
                _commandQueue.Writer.TryWrite( (_commandAwaker, default, false) );
                monitor.Info( $"Canceled {cRemoved} waiting commands." );
            }
            int dRemoved = 0;
            if( cancelDeferredCommands )
            {
                var d = _deferredCommands;
                _deferredCommands = new Queue<(BaseDeviceCommand Command, CancellationToken Token, bool CheckKey)>();
                dRemoved = d.Count;
                monitor.Info( $"Canceled {dRemoved} deferred commands." );
                while( d.TryDequeue( out var c ) )
                {
                    c.Command.InternalCompletion.SetCanceled();
                }
            }
            return (cRemoved, dRemoved);
        }

        /// <inheritdoc />
        public bool SendCommand( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName );
            monitor.Debug( $"SendCommand: {command}." );
            return SendRoutedCommand( command, token, checkControllerKey );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommand( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false );
            monitor.Debug( $"UnsafeSendCommand: {command}." );
            return SendRoutedCommand( command, token, false );
        }

        /// <summary>
        /// Sends the given command directly in the waiting queue.
        /// This is to be used for low level internal commands, typically initiated by timers.
        /// </summary>
        /// <param name="command">The command to send without any checks.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <param name="checkControllerKey">Optionally checks the ControllerKey.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        internal protected bool SendRoutedCommand( BaseDeviceCommand command, CancellationToken token = default, bool checkControllerKey = false )
        {
            return _commandQueue.Writer.TryWrite( (command, token, checkControllerKey ) );
        }

        /// <inheritdoc />
        public bool SendCommandImmediate( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName );
            monitor.Debug( $"SendCommandImmediate: '{command}'." );
            return SendRoutedCommandImmediate( command, token, checkControllerKey );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommandImmediate( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false );
            monitor.Debug( $"UnsafeSendCommandImmediate: '{command}'." );
            return SendRoutedCommandImmediate( command, token, false );
        }

        /// <summary>
        /// Sends the given command directly for immediate execution.
        /// This is to be used for low level internal commands, typically initiated by timers.
        /// </summary>
        /// <param name="command">The command to send without any checks.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <param name="checkControllerKey">Optionally checks the ControllerKey.</param>
        /// <returns>True on success, false if this device doesn't accept commands anymore since it is destroyed.</returns>
        internal protected bool SendRoutedCommandImmediate( BaseDeviceCommand command, CancellationToken token = default, bool checkControllerKey = false )
        {
            return _commandQueueImmediate.Writer.TryWrite( (command, token, checkControllerKey) ) && _commandQueue.Writer.TryWrite( (_commandAwaker, default, false) );
        }

        void CheckDirectCommandParameter( IActivityMonitor monitor, BaseDeviceCommand command, bool checkDeviceName )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( _host!.GetType() ) ) throw new ArgumentException( $"{command.GetType().Name}: Invalid HostType '{command.HostType.Name}'.", nameof( command ) );
            if( !command.CheckValidity( monitor ) ) throw new ArgumentException( $"{command.GetType().Name}: CheckValidity failed. See logs.", nameof( command ) );
            if( checkDeviceName )
            {
                if( command.DeviceName != Name )
                {
                    throw new ArgumentException( $"{command.GetType().Name}: Command DeviceName is '{command.DeviceName}', device '{Name}' cannot execute it. (For direct execution, you can use checkDeviceName: false parameter to skip this check or use UnsafeSendCommand.)", nameof( command ) );
                }
            }
            else
            {
                command.DeviceName = Name;
            }
        }

        async Task CommandRunLoop()
        {
            bool wasStop = true;
            while( !IsDestroyed )
            {
                BaseDeviceCommand? currentlyExecuting = null;
                (BaseDeviceCommand cmd, CancellationToken token, bool checkKey) = (null!, default, false);
                try
                {
                    (cmd, token, checkKey) = await _commandQueue.Reader.ReadAsync().ConfigureAwait( false );
                    if( _commandQueueImmediate.Reader.TryRead( out var immediate ) )
                    {
                        do
                        {
                            currentlyExecuting = immediate.Command;
                            _commandMonitor.Debug( $"Command '{currentlyExecuting}' has been sent as Immediate. Handling it now." );
                            await HandleCommandAsync( currentlyExecuting, immediate.Token, immediate.CheckKey, allowDefer: false ).ConfigureAwait( false );
                        }
                        while( !IsDestroyed && _commandQueueImmediate.Reader.TryRead( out immediate ) );
                        if( IsDestroyed ) break;
                    }

                    // Captures the reference (since it can be replaced by CancelAllPendingCommands).
                    var deferred = _deferredCommands;
                    if( wasStop && IsRunning && deferred.Count > 0 )
                    {
                        using( _commandMonitor.OpenDebug( $"Device started: executing {deferred.Count} deferred commands." ) )
                        {
                            while( IsRunning && deferred.TryDequeue( out var ct ) )
                            {
                                currentlyExecuting = ct.Command;
                                _commandMonitor.Debug( $"Command '{currentlyExecuting}' has been deferred. Handling it now." );
                                await HandleCommandAsync( currentlyExecuting, ct.Token, ct.CheckKey, allowDefer: false ).ConfigureAwait( false );
                            }
                        }
                        if( IsDestroyed ) break;
                    }
                    if( cmd == _commandAwaker ) continue;
                    currentlyExecuting = cmd;
                    wasStop = !IsRunning;

                    await HandleCommandAsync( cmd, token, checkKey, allowDefer: true ).ConfigureAwait( false );
                }
                catch( Exception ex )
                {
                    Debug.Assert( currentlyExecuting != null );
                    using( _commandMonitor.OpenError( $"Unhandled error in '{FullName}' while processing '{currentlyExecuting}'.", ex ) )
                    {
                        if( !currentlyExecuting.InternalCompletion.TrySetException( ex ) )
                        {
                            _commandMonitor.Warn( $"Command has already been completed. Unable to set the error." );
                        }
                        bool mustStop = true;
                        try
                        {
                            mustStop = await OnCommandErrorAsync( _commandMonitor, currentlyExecuting, ex ).ConfigureAwait( false );
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
            _commandQueue.Writer.Complete();
            _commandQueueImmediate.Writer.Complete();
            _commandMonitor.Info( $"Ending device loop, flushing command queues by signaling a UnavailableDeviceException." );
            while( _commandQueueImmediate.Reader.TryRead( out var cmd ) )
            {
                cmd.Command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd.Command ) );
            }
            while( _deferredCommands.TryDequeue( out var cmd ) )
            {
                cmd.Command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd.Command ) );
            }
            while( _commandQueue.Reader.TryRead( out var cmd ) )
            {
                cmd.Command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, cmd.Command ) );
            }
            _commandMonitor.MonitorEnd();
        }

        async Task HandleCommandAutoStartAsync( BaseDeviceCommand command, bool withStop, CancellationToken token )
        {
            _commandMonitor.Debug( $"Starting command '{command}' handling and {(withStop ? "stopping it after" : "let it run")}." );
            Debug.Assert( !IsRunning );
            await HandleStartAsync( null, withStop ? DeviceStartedReason.SilentAutoStartAndStopStoppedBehavior : DeviceStartedReason.StartAndKeepRunningStoppedBehavior ).ConfigureAwait( false );
            if( IsRunning )
            {
                try
                {
                    await DoHandleCommandAsync( _commandMonitor, command, token ).ConfigureAwait( false );
                    if( IsRunning )
                    {
                        // Awake the queue for deferred commands.
                        _commandQueue.Writer.TryWrite( (_commandAwaker, default, false) );
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
                }
            }
            else
            {
                _commandMonitor.Debug( "Failed to start. Canceling the AutoStarting command." );
            }
        }
        async Task HandleCommandAsync( BaseDeviceCommand command, CancellationToken token, bool checkKey, bool allowDefer )
        {
            if( token.IsCancellationRequested )
            {
                _commandMonitor.Debug( "Canceling command because token.IsCancellationRequested." );
                command.InternalCompletion.TrySetCanceled();
                return;
            }
            // Basic commands are all by design AlwaysRunning: no call to OnStoppedBehavior must be made,
            // but controller key must be checked for each of them.
            // The check is repeated instead of duplicating the switch.
            switch( command )
            {
                case BaseStopDeviceCommand stop:
                    if( checkKey && !CheckControllerKey( command ) ) return;
                    await HandleStopAsync( stop, DeviceStoppedReason.StoppedCall ).ConfigureAwait( false );
                    return;
                case BaseStartDeviceCommand start:
                    if( checkKey && !CheckControllerKey( command ) ) return;
                    await HandleStartAsync( start, DeviceStartedReason.StartCall ).ConfigureAwait( false );
                    return;
                case BaseReconfigureDeviceCommand<TConfiguration> config:
                    if( checkKey && !CheckControllerKey( command ) ) return;
                    await HandleReconfigureAsync( config, token ).ConfigureAwait( false );
                    return;
                case BaseSetControllerKeyDeviceCommand setC:
                    if( checkKey && !CheckControllerKey( command ) ) return;
                    await HandleSetControllerKeyAsync( setC ).ConfigureAwait( false );
                    return;
                case BaseDestroyDeviceCommand destroy:
                    if( checkKey && !CheckControllerKey( command ) ) return;
                    await HandleDestroyAsync( destroy, false ).ConfigureAwait( false );
                    return;
                default:
                {
                    if( !IsRunning )
                    {
                        using( _commandMonitor.OpenDebug( $"Handling command '{command}' while device is stopped with Command.StoppedBehavior = '{command.StoppedBehavior}'." ) )
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
                                    if( checkKey && !CheckControllerKey( command ) ) return;
                                    await HandleCommandAutoStartAsync( command, withStop: command.StoppedBehavior == DeviceCommandStoppedBehavior.SilentAutoStartAndStop, token );
                                    return;
                                case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel:
                                    if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                                    {
                                        _commandMonitor.CloseGroup( $"Pushing command to the deferred command queue." );
                                        _deferredCommands.Enqueue( (command, token, checkKey) );
                                    }
                                    else
                                    {
                                        _commandMonitor.CloseGroup( $"Canceling command." );
                                        command.InternalCompletion.TrySetCanceled();
                                    }
                                    return;
                                case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrSetUnavailableDeviceException:
                                    if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                                    {
                                        _commandMonitor.CloseGroup( $"Pushing command to the deferred command queue." );
                                        _deferredCommands.Enqueue( (command, token, checkKey) );
                                    }
                                    else
                                    {
                                        _commandMonitor.CloseGroup( $"Setting UnavailableDeviceException on command." );
                                        command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                    }
                                    return;
                                case DeviceCommandStoppedBehavior.SetUnavailableDeviceException:
                                    _commandMonitor.CloseGroup( $"Setting UnavailableDeviceException on command." );
                                    command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                    return;
                                case DeviceCommandStoppedBehavior.Cancel:
                                    _commandMonitor.CloseGroup( $"Canceling command." );
                                    command.InternalCompletion.TrySetCanceled();
                                    return;
                                case DeviceCommandStoppedBehavior.AlwaysWaitForNextStart:
                                    if( allowDefer )
                                    {
                                        _commandMonitor.CloseGroup( $"Pushing command to the deferred command queue." );
                                        _deferredCommands.Enqueue( (command, token, checkKey) );
                                    }
                                    else
                                    {
                                        _commandMonitor.CloseGroup( $"Setting UnavailableDeviceException on command." );
                                        command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                                    }
                                    return;
                                case DeviceCommandStoppedBehavior.RunAnyway:
                                    _commandMonitor.CloseGroup( $"Let the command be handled anyway." );
                                    break;
                                default: throw new NotSupportedException( "Unknown DeviceCommandStoppedBehavior." );
                            }
                        }
                    }
                    using( _commandMonitor.OpenDebug( $"Handling command '{command}'." ) )
                    {
                        if( checkKey && !CheckControllerKey( command ) ) return;
                        await DoHandleCommandAsync( _commandMonitor, command, token ).ConfigureAwait( false );
                    }
                    break;
                }
            };
        }

        bool CheckControllerKey( BaseDeviceCommand command )
        {
            var key = ControllerKey;
            if( key != null && command.ControllerKey != key )
            {
                var msg = $"{command.GetType().Name}: Expected command ControllerKey is '{command.ControllerKey}' but current device's one is '{key}'. (You can use checkControllerKey: false parameter to skip this check or use UnsafeSendCommand.)";
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
        /// Extension point that can be overridden to avoid calling <see cref="IDevice.StopAsync(IActivityMonitor, bool)"/> (that ignores
        /// the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration), when the handling of a command raised an exception,
        /// and/or to emit an event (typically an UnexpectedErrorEvent).
        /// <para>
        /// This default implementation returns always true: the device is stopped by default.
        /// </para>
        /// <para>
        /// Specialized implementations can call <see cref="IDevice.StopAsync(IActivityMonitor, bool)"/> (or even
        /// <see cref="IDevice.DestroyAsync(IActivityMonitor)"/>) directly if needed.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The culprit command.</param>
        /// <param name="ex">The exception raised.</param>
        /// <returns>True to stop this device, false to let this device run.</returns>
        protected virtual Task<bool> OnCommandErrorAsync( IActivityMonitor monitor, BaseDeviceCommand command, Exception ex ) => Task.FromResult( true );

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
        /// <param name="token">Cancellation token.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
        {
            // By returning a faulty task here, we'll enter the catch clause of the command execution 
            // and the command's TCS will be set with the exception.
            return Task.FromException( new NotSupportedException( $"Unhandled command type: '{command.GetType().FullName}'." ) );
        }


    }
}