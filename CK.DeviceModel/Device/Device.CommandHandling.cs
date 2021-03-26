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
        readonly Channel<(DeviceCommandBase Command, CancellationToken Token, bool CheckKey)> _commandQueue;
        readonly Queue<(DeviceCommandBase Command, CancellationToken Token, bool CheckKey)> _deferredCommands;

        readonly Channel<(DeviceCommandBase Command, CancellationToken Token, bool CheckKey)> _commandQueueImmediate;

        /// <summary>
        /// Dummy command that is used to awake the command loop.
        /// This does nothing and is ignored except that, just like any other commands that are
        /// dequeued, this handles the _commandQueueImmediate execution.
        /// </summary>
        class CommandAwaker : DeviceCommandBase
        {
            public override Type HostType => throw new NotImplementedException();
            internal override ICommandCompletionSource InternalCompletion => throw new NotImplementedException();
            protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
        }
        static readonly CommandAwaker _commandAwaker = new();

        /// <inheritdoc />
        public bool SendCommand( IActivityMonitor monitor, DeviceCommandBase command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName );
            return SendRoutedCommand( command, token, checkControllerKey );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommand( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false );
            return SendRoutedCommand( command, token, false );
        }

        internal bool SendRoutedCommand( DeviceCommandBase command, CancellationToken token, bool checkControllerKey )
        {
            return _commandQueue.Writer.TryWrite( (command, token, checkControllerKey ) );
        }

        public bool SendCommandImmediate( IActivityMonitor monitor, DeviceCommandBase command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName );
            return SendRoutedCommandImmediate( command, token, checkControllerKey );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommandImmediate( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false );
            return SendRoutedCommandImmediate( command, token, false );
        }

        internal bool SendRoutedCommandImmediate( DeviceCommandBase command, CancellationToken token, bool checkControllerKey )
        {
            return _commandQueueImmediate.Writer.TryWrite( (command, token, checkControllerKey) ) && _commandQueue.Writer.TryWrite( (_commandAwaker, default, false) );
        }

        void CheckDirectCommandParameter( IActivityMonitor monitor, DeviceCommandBase command, bool checkDeviceName )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( _host!.GetType() ) ) throw new ArgumentException( $"{command.GetType().Name}: Invalid HostType '{command.HostType.Name}'.", nameof( command ) );
            if( !command.CheckValidity( monitor ) ) throw new ArgumentException( $"{command.GetType().Name}: CheckValidity failed. See logs.", nameof( command ) );
            if( checkDeviceName && command.DeviceName != Name )
            {
                throw new ArgumentException( $"{command.GetType().Name}: Command DeviceName is '{command.DeviceName}', device '{Name}' cannot execute it. (For direct execution, you can use checkDeviceName: false parameter to skip this check or use UnsafeSendCommand.)", nameof(command) );
            }
        }

        async Task CommandRunLoop()
        {
            bool wasStop = true;
            while( !IsDestroyed )
            {
                DeviceCommandBase? currentlyExecuting = null;
                (DeviceCommandBase cmd, CancellationToken token, bool checkKey) = (null!, default, false);
                try
                {
                    (cmd, token, checkKey) = await _commandQueue.Reader.ReadAsync();
                    while( _commandQueueImmediate.Reader.TryRead( out var immediate ) )
                    {
                        currentlyExecuting = immediate.Command;
                        await HandleCommandAsync( currentlyExecuting, immediate.Token, immediate.CheckKey, allowDefer: false );
                    }
                    if( cmd == _commandAwaker ) continue;

                    if( wasStop && IsRunning && _deferredCommands.Count > 0 )
                    {
                        using( _commandMonitor.OpenDebug( $"Device started: executing {_deferredCommands.Count} deferred commands." ) )
                        {
                            while( IsRunning && _deferredCommands.TryDequeue( out var ct ) )
                            {
                                currentlyExecuting = ct.Command;
                                await HandleCommandAsync( currentlyExecuting, ct.Token, ct.CheckKey, allowDefer: false );
                            }
                        }
                    }
                    wasStop = !IsRunning;
                    currentlyExecuting = cmd;
                    await HandleCommandAsync( cmd, token, checkKey, allowDefer: true );
                }
                catch( Exception ex )
                {
                    Debug.Assert( currentlyExecuting != null );
                    using( _commandMonitor.OpenError( $"Unhandled error in '{FullName}' while processing '{currentlyExecuting.GetType().Name}'.", ex ) )
                    {
                        bool mustStop = true;
                        try
                        {
                            if( !currentlyExecuting.InternalCompletion.TrySetException( ex ) )
                            {
                                _commandMonitor.Warn( $"Command has already been completed. Unable to set the error." );
                            }
                            mustStop = await OnCommandErrorAsync( _commandMonitor, currentlyExecuting, ex );
                        }
                        catch( Exception ex2 )
                        {
                            _commandMonitor.Fatal( $"Device '{FullName}' OnCommandErrorAsync raised an error. Device will stop.", ex2 );
                        }
                        if( mustStop )
                        {
                            await StopAsync( _commandMonitor, ignoreAlwaysRunning: true );
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

        Task HandleCommandAsync( DeviceCommandBase command, CancellationToken token, bool checkKey, bool allowDefer )
        {
            if( token.IsCancellationRequested )
            {
                command.InternalCompletion.TrySetCanceled();
                return Task.CompletedTask;
            }
            if( !IsRunning && command.StoppedBehavior != DeviceCommandStoppedBehavior.RunAnyway )
            {
                switch( OnStoppedDeviceCommand( _commandMonitor, command ) )
                {
                    case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel:
                        if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                        {
                            _deferredCommands.Enqueue( (command, token, checkKey) );
                        }
                        else
                        {
                            command.InternalCompletion.TrySetCanceled();
                        }
                        return Task.CompletedTask;
                    case DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrSetDeviceStoppedException:
                        if( allowDefer && _configStatus == DeviceConfigurationStatus.AlwaysRunning )
                        {
                            _deferredCommands.Enqueue( (command, token, checkKey) );
                        }
                        else
                        {
                            command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                        }
                        return Task.CompletedTask;
                    case DeviceCommandStoppedBehavior.SetDeviceStoppedException:
                        command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                        return Task.CompletedTask;
                    case DeviceCommandStoppedBehavior.Cancel:
                        command.InternalCompletion.TrySetCanceled();
                        return Task.CompletedTask;
                    case DeviceCommandStoppedBehavior.AlwaysWaitForNextStart:
                        if( allowDefer )
                        {
                            _deferredCommands.Enqueue( (command, token, checkKey) );
                        }
                        else
                        {
                            command.InternalCompletion.TrySetException( new UnavailableDeviceException( this, command ) );
                        }
                        return Task.CompletedTask;
                    case DeviceCommandStoppedBehavior.RunAnyway:
                        break;
                    default: return Task.FromException( new NotSupportedException( "Unknown DeviceCommandStoppedBehavior." ) );
                }
            }
            if( checkKey )
            {
                var key = ControllerKey;
                if( key != null && command.ControllerKey != key )
                {
                    var msg = $"{command.GetType().Name}: Expected command ControllerKey is '{command.ControllerKey}' but current device's one is '{key}'. (You can use checkControllerKey: false parameter to skip this check or use UnsafeSendCommand.)";
                    _commandMonitor.Error( msg );
                    command.InternalCompletion.TrySetException( new InvalidControllerKeyException( msg ) );
                    return Task.CompletedTask;
                }
            }
            return command switch
            {
                StopDeviceCommand stop => HandleStopAsync( stop, DeviceStoppedReason.StoppedCall ),
                StartDeviceCommand start => HandleStartAsync( start, DeviceStartedReason.StartCall ),
                ReconfigureDeviceCommand<TConfiguration> config => HandleReconfigureAsync( config, token ),
                DestroyDeviceCommand destroy => HandleDestroyAsync( destroy, false ),
                _ => DoHandleCommandAsync( _commandMonitor, command, token )
            };
        }

        /// <summary>
        /// Extension point that is called for each command that must be executed while this device is stopped.
        /// This default implementation simply returns the <see cref="DeviceCommandBase.StoppedBehavior"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command that should be executed, deferred, canceled or set on error.</param>
        /// <returns>The behavior to apply for the command.</returns>
        protected virtual DeviceCommandStoppedBehavior OnStoppedDeviceCommand( IActivityMonitor monitor, DeviceCommandBase command )
        {
            return command.StoppedBehavior;
        }

        /// <summary>
        /// Extension point that can be overridden to avoid calling <see cref="Device{TConfiguration}.AutoStopAsync(IActivityMonitor, bool)"/> (ignoring
        /// the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration), when the handling of a command raised an exception.
        /// <para>
        /// This default implementation returns always true: the device is stopped by default.
        /// Specialized implementations can call <see cref="Device{TConfiguration}.AutoStopAsync(IActivityMonitor, bool)"/> (or even
        /// <see cref="Device{TConfiguration}.AutoDestroyAsync(IActivityMonitor)"/>) themselves and in this case, false should be returned.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The culprit command.</param>
        /// <param name="ex">The exception raised.</param>
        /// <returns>True to stop this device, false to let this device run.</returns>
        protected virtual Task<bool> OnCommandErrorAsync( IActivityMonitor monitor, DeviceCommandBase command, Exception ex ) => Task.FromResult( true );

        /// <summary>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
        /// <para>
        /// The <paramref name="command"/> object that is targeted to this device (<see cref="DeviceCommand.DeviceName"/> matches <see cref="IDevice.Name"/>
        /// and <see cref="DeviceCommand.ControllerKey"/> is either null or match the current <see cref="ControllerKey"/>).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task DoHandleCommandAsync( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token )
        {
            // By returning a faulty task here, we'll enter the catch clause of the command execution 
            // and the command's TCS will be set with the exception.
            return Task.FromException( new ArgumentException( $"Unhandled command {command.GetType().Name}.", nameof( command ) ) );
        }


    }
}
