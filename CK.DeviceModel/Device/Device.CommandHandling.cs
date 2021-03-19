using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public abstract partial class Device<TConfiguration>
    {
        readonly Channel<(DeviceCommandBase Command,CancellationToken Token)> _commandQueue;
        readonly Queue<(DeviceCommandBase Command, CancellationToken Token)> _deferredCommands;

        DeviceCommandStoppedBehavior _defaultStoppedBehavior;

        /// <summary>
        /// Gets the default behavior to apply when a command is handled while this device is stopped.
        /// </summary>
        protected DeviceCommandStoppedBehavior DefaultStoppedBehavior => _defaultStoppedBehavior;

        /// <inheritdoc />
        public bool SendCommand( IActivityMonitor monitor, DeviceCommandBase command, bool checkDeviceName = true, bool checkControllerKey = true, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, checkDeviceName, checkControllerKey );
            return _commandQueue.Writer.TryWrite( (command, token) );
        }

        /// <inheritdoc />
        public bool UnsafeSendCommand( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token = default )
        {
            CheckDirectCommandParameter( monitor, command, false, false );
            return _commandQueue.Writer.TryWrite( (command, token) );
        }

        void CheckDirectCommandParameter( IActivityMonitor monitor, DeviceCommandBase command, bool checkDeviceName, bool checkControllerKey )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            if( !command.HostType.IsAssignableFrom( _host!.GetType() ) ) throw new ArgumentException( $"{command.GetType().Name}: Invalid HostType '{command.HostType.Name}'.", nameof( command ) );
            if( !command.CheckValidity( monitor ) ) throw new ArgumentException( $"{command.GetType().Name}: CheckValidity failed. See logs.", nameof( command ) );
            if( checkDeviceName && command.DeviceName != Name ) throw new ArgumentException( $"{command.GetType().Name}: Command DeviceName is '{command.DeviceName}', device '{Name}' cannot execute it. (For direct execution, you can use checkDeviceName: false parameter to skip this check.)", nameof( command ) );
            if( checkControllerKey )
            {
                var invalidKey = CheckCommandControllerKey( command );
                if( invalidKey != null ) throw new ArgumentException( $"{command.GetType().Name}: {invalidKey}" );
            }
        }

        internal string? CheckCommandControllerKey( DeviceCommandBase command )
        {
            // The basic ResetControllerKey command sets a new ControllerKey.
            if( command is not BasicControlDeviceCommand b || b.Operation != BasicControlDeviceOperation.ResetControllerKey )
            {
                var key = ControllerKey;
                if( key != null && command.ControllerKey != key )
                {
                    return $"Expected command ControllerKey is '{command.ControllerKey}' but current device's one is '{key}'. (For direct execution, you can use checkControllerKey: false parameter to skip this check.)";
                }
            }
            return null;
        }

        /// <summary>
        /// Extension point that is called for each command that must be executed while this device is stopped.
        /// This default implementation simply returns the configured <see cref="DeviceConfiguration.DefaultStoppedBehavior"/>.
        /// <para>
        /// This method may return null (nothing will be done): this suppose that the method has manually handled the case
        /// by setting the appropriate final result on <see cref="DeviceCommand.Result"/> or <see cref="DeviceCommand{TResult}.Result"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command that should be executed, deferred, canceled or set on error.</param>
        /// <returns>The behavior to apply for the command.</returns>
        protected virtual Task<DeviceCommandStoppedBehavior?> OnStoppedDeviceCommandAsync( IActivityMonitor monitor, DeviceCommandBase command ) => Task.FromResult<DeviceCommandStoppedBehavior?>( _defaultStoppedBehavior );

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
        protected virtual Task<bool> OnCommandErrorAsync( ActivityMonitor monitor, DeviceCommandBase command, Exception ex ) => Task.FromResult( true );

        async Task CommandRunLoop()
        {
            var monitor = new ActivityMonitor( $"Command loop for device {FullName}." );
            bool isRunning = false;
            for(; ; )
            {
                (DeviceCommandBase cmd, CancellationToken token) = (null!, default);
                try
                {
                    (cmd,token) = await _commandQueue.Reader.ReadAsync( DestroyToken );
                    if( token.IsCancellationRequested )
                    {
                        cmd.GetCompletionResult().SetCanceled();
                    }
                    else if( cmd is BasicControlDeviceCommand b )
                    {
                        try
                        {
                            await ExecuteBasicControlDeviceCommandAsync( monitor, b );
                            b.Result.SetSuccess();
                        }
                        catch( Exception ex )
                        {
                            b.Result.SetError( ex );
                        }
                    }
                    else
                    {
                        if( !isRunning && IsRunning )
                        {
                            if( _deferredCommands.Count > 0 )
                            {
                                using( monitor.OpenDebug( $"Device started: executing {_deferredCommands.Count} deferred commands." ) )
                                {
                                    while( _deferredCommands.TryPeek( out var ct ) )
                                    {
                                        if( ct.Token.IsCancellationRequested )
                                        {
                                            ct.Command.GetCompletionResult().SetCanceled();
                                            _deferredCommands.Dequeue();
                                        }
                                        else
                                        {
                                            using( var runLock = GetRunningLock() )
                                            {
                                                if( IsRunning )
                                                {
                                                    await DoHandleCommandAsync( monitor, ct.Command, ct.Token );
                                                if( !isRunning ) break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        bool mustExecute = true;
                        isRunning = IsRunning;
                        if( !isRunning )
                        {
                            switch( await OnStoppedDeviceCommandAsync( monitor, cmd ) )
                            {
                                case null:
                                    mustExecute = false;
                                    break;
                                case DeviceCommandStoppedBehavior.SetDeviceStoppedException:
                                    mustExecute = false;
                                    cmd.GetCompletionResult().SetError( new DeviceStoppedException( this, cmd ) );
                                    break;
                                case DeviceCommandStoppedBehavior.Cancelled:
                                    mustExecute = false;
                                    cmd.GetCompletionResult().SetCanceled();
                                    break;
                                case DeviceCommandStoppedBehavior.WaitForNextStart:
                                    mustExecute = false;
                                    _deferredCommands.Enqueue( (cmd,token) );
                                    break;
                            }
                        }
                        if( mustExecute )
                        {
                            await DoHandleCommandAsync( monitor, cmd, token );
                        }
                    }
                    if( IsDestroyed )
                    {
                        break;
                    }
                }
                catch( Exception ex )
                {
                    // If we are being destroyed, simply breaks the loop.
                    if( ex is OperationCanceledException && DestroyToken.IsCancellationRequested ) break;
                    using( monitor.OpenError( $"Unhandled error in '{FullName}'.", ex ) )
                    {
                        bool mustStop = true;
                        try
                        {
                            mustStop = await OnCommandErrorAsync( monitor, cmd, ex );
                        }
                        catch( Exception ex2 )
                        {
                            monitor.Fatal( $"Device '{FullName}' OnCommandErrorAsync raised an error.", ex2 );
                        }
                        if( mustStop )
                        {
                            await AutoStopAsync( monitor, ignoreAlwaysRunning: true );
                        }
                    }
                }
            }
            _commandQueue.Writer.Complete();
            monitor.Info( $"Ending device loop, flushing command queue by signaling a DeviceStoppedException." );
            while( _commandQueue.Reader.TryRead( out var cmd ) )
            {
                cmd.Command.GetCompletionResult().SetError( new DeviceStoppedException( this, cmd.Command ) );
            }
            monitor.MonitorEnd();
        }


        Task ExecuteBasicControlDeviceCommandAsync( IActivityMonitor monitor, BasicControlDeviceCommand b )
        {
            return b.Operation switch
            {
                BasicControlDeviceOperation.Start => StartAsync( monitor ),
                BasicControlDeviceOperation.Stop => StopAsync( monitor ),
                BasicControlDeviceOperation.ResetControllerKey => SetControllerKeyAsync( monitor, b.ControllerKey ),
                BasicControlDeviceOperation.None => Task.CompletedTask,
                _ => Task.FromException( new ArgumentOutOfRangeException( nameof( BasicControlDeviceCommand.Operation ) ) ),
            };
        }


    }
}
