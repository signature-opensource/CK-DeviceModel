using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Standard implementation that queues the incoming commands: execution is
    /// serialized and a dedicated <see cref="ActivityMonitor"/> is created.
    /// </summary>
    /// <typeparam name="TConfiguration"></typeparam>
    public abstract partial class StdDevice<TConfiguration> : Device<TConfiguration> where TConfiguration : StdDeviceConfiguration
    {
        readonly Channel<QueuedCommand> _commandQueue;
        readonly Queue<QueuedCommand> _deferredCommands;

        readonly StoppedBehavior _stoppedBehavior;

        #region Queued commands
        /// <summary>
        /// Base class for commands that have no result and don't need to be awaited.
        /// </summary>
        class QueuedCommand
        {
            public readonly CancellationToken Token;
            readonly DeviceCommand _command;

            public QueuedCommand( DeviceCommand c, CancellationToken t )
            {
                _command = c;
                Token = t;
            }

            public DeviceCommand Command => _command;

            public virtual Task ExecuteAsync( StdDevice<TConfiguration> device, IActivityMonitor monitor ) => device.HandleCommandAsync( monitor, _command, Token );

            public virtual void SetException( Exception ex ) { }

            public virtual void SetCancelled() { }
        }

        /// <summary>
        /// Commands that have no result but need to be awaited.
        /// </summary>
        class WaitableCommand : QueuedCommand
        {
            readonly TaskCompletionSource<bool> TCS;

            public WaitableCommand( DeviceCommand c, CancellationToken t, TaskCompletionSource<bool> tcs )
                : base( c, t )
            {
                TCS = tcs;
            }

            public override Task ExecuteAsync( StdDevice<TConfiguration> device, IActivityMonitor monitor )
            {
                return device.HandleCommandAsync( monitor, Command, Token ).ContinueWith( Continuation, TaskContinuationOptions.ExecuteSynchronously );
            }

            void Continuation( Task t )
            {
                switch( t.Status )
                {
                    case TaskStatus.Canceled: TCS.SetCanceled(); break;
                    case TaskStatus.Faulted: TCS.SetException( t.Exception ); break;
                    default: TCS.SetResult( true ); break;
                }
            }

            public override void SetException( Exception ex ) => TCS.SetException( ex );

            public override void SetCancelled() => TCS.SetCanceled();

        }

        /// <summary>
        /// Commands that have no result but need to be awaited.
        /// </summary>
        class WaitableCommand<TResult> : QueuedCommand
        {
            readonly TaskCompletionSource<TResult> TCS;

            public WaitableCommand( DeviceCommand<TResult> c, CancellationToken t, TaskCompletionSource<TResult> tcs )
                : base( c, t )
            {
                TCS = tcs;
            }

            protected new DeviceCommand<TResult> Command => (DeviceCommand<TResult>)base.Command;

            public override Task ExecuteAsync( StdDevice<TConfiguration> device, IActivityMonitor monitor )
            {
                return device.HandleCommandAsync( monitor, Command, Token ).ContinueWith( Continuation, TaskContinuationOptions.ExecuteSynchronously );
            }

            void Continuation( Task<TResult> t )
            {
                switch( t.Status )
                {
                    case TaskStatus.Canceled: TCS.SetCanceled(); break;
                    case TaskStatus.Faulted: TCS.SetException( t.Exception ); break;
                    default: TCS.SetResult( t.Result ); break;
                }
            }

            public override void SetException( Exception ex ) => TCS.SetException( ex );

            public override void SetCancelled() => TCS.SetCanceled();
        }
        #endregion

        /// <summary>
        /// Initializes a new standard device bound to a configuration.
        /// Concrete device must expose a constructor with the exact same signature: initial configuration is handled by
        /// this constructor, warnings or errors must be logged and exception can be thrown if anything goes wrong. 
        /// </summary>
        /// <param name="monitor">
        /// The monitor to use for the initialization phase. A reference to this monitor must not be kept.
        /// </param>
        /// <param name="info">
        /// Contains the initial configuration to use. It must be <see cref="DeviceConfiguration.CheckValid"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </param>
        /// <param name="defaultStoppedBehavior">
        /// Configures the return of the default <see cref="OnStoppedDeviceCommandAsync(ActivityMonitor, DeviceCommand)"/> implementation.
        /// If differences must be made across commands, the method can be overridden but for most use case, configuring the default behavior
        /// here is enough.
        /// </param>
        protected StdDevice( IActivityMonitor monitor, CreateInfo info, StoppedBehavior defaultStoppedBehavior = StoppedBehavior.SetNotRunningException )
            : base( monitor, info )
        {
            _commandQueue = Channel.CreateUnbounded<QueuedCommand>( new UnboundedChannelOptions() { SingleReader = true } );
            _deferredCommands = new Queue<QueuedCommand>();
            _stoppedBehavior = defaultStoppedBehavior;
            _ = Task.Run( RunLoop );
        }

        /// <summary>
        /// Overridden and sealed handling of commands without result.
        /// Commands are queued and executed on the internal command loop.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The awaitable.</returns>
        protected sealed override Task DoHandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token )
        {
            var cts = new TaskCompletionSource<bool>();
            var error = DoEnqueue( new WaitableCommand( command, token, cts ) );
            if( error != null ) cts.SetException( error );
            return cts.Task;
        }

        /// <summary>
        /// Overridden and sealed handling of commands that generate a result.
        /// Commands are queued and executed on the internal command loop.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The command result.</returns>
        protected sealed override Task<TResult> DoHandleCommandAsync<TResult>( IActivityMonitor monitor, DeviceCommand<TResult> command, CancellationToken token )
        {
            var cts = new TaskCompletionSource<TResult>();
            var error = DoEnqueue( new WaitableCommand<TResult>( command, token, cts ) );
            if( error != null ) cts.SetException( error );
            return cts.Task;
        }

        Exception? DoEnqueue( StdDevice<TConfiguration>.QueuedCommand c )
        {
            if( !_commandQueue.Writer.TryWrite( c ) )
            {
                return new CKException( $"Device '{FullName}' (Status: {Status}) cannot accept commands anymore." );
            }
            return null;
        }

        async Task RunLoop()
        {
            var monitor = new ActivityMonitor( $"Command loop for device {FullName}." );
            bool isRunning = false;
            for( ; ; )
            {
                QueuedCommand cmd = null!;
                try
                {
                    cmd = await _commandQueue.Reader.ReadAsync( DestroyToken );
                    if( cmd.Token.IsCancellationRequested )
                    {
                        cmd.SetCancelled();
                    }
                    else if( cmd.Command is BasicControlDeviceCommand b )
                    {
                        await ExecuteBasicControlDeviceCommandAsync( monitor, b );
                    }
                    else
                    {
                        if( !isRunning && IsRunning )
                        {
                            if( _deferredCommands.Count > 0 )
                            {
                                using( monitor.OpenDebug( $"Device started: executing {_deferredCommands.Count} deferred commands." ) )
                                {
                                    while( _deferredCommands.TryDequeue( out var c ) )
                                    {
                                        await c.ExecuteAsync( this, monitor );
                                    }
                                }
                            }
                        }
                        bool mustExecute = true;
                        isRunning = IsRunning;
                        if( !isRunning )
                        {
                            switch( OnStoppedDeviceCommand( monitor, cmd.Command ) )
                            {
                                case StoppedBehavior.SetNotRunningException:
                                    mustExecute = false;
                                    SetNotRunningException( cmd );
                                    break;
                                case StoppedBehavior.Cancelled:
                                    mustExecute = false;
                                    cmd.SetCancelled();
                                    break;
                                case StoppedBehavior.WaitForNextStart:
                                    mustExecute = false;
                                    _deferredCommands.Enqueue( cmd );
                                    break;
                            }
                        }
                        if( mustExecute )
                        {
                            await cmd.ExecuteAsync( this, monitor );
                        }
                    }
                    if( Status.IsDestroyed )
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
                            mustStop = await OnCommandErrorAsync( monitor, cmd.Command, ex );
                        }
                        catch( Exception ex2 )
                        {
                            monitor.Fatal( $"Device '{FullName}' OnUnhandledError raised an error.", ex2 );
                        }
                        if( mustStop )
                        {
                            await AutoStopAsync( monitor, ignoreAlwaysRunning: true );
                        }
                    }
                }
            }
            _commandQueue.Writer.Complete();
            monitor.Info( $"Ending device loop, flushing command queue by signaling a destroyed exception." );
            while( _commandQueue.Reader.TryRead( out var cmd ) )
            {
                SetNotRunningException( cmd );
            }
            monitor.MonitorEnd();

            void SetNotRunningException( StdDevice<TConfiguration>.QueuedCommand cmd )
            {
                cmd.SetException( new CKException( $"Unable to execute command '{cmd.Command.GetType().Name}' on device '{FullName}', its status is {Status}." ) );
            }
        }

        /// <summary>
        /// Sends a command and don't await any result. This can be called for any command, including <see cref="DeviceCommand{TResult}"/> that
        /// generate a result.
        /// <para>
        /// Commands pushed in the internal queue by this method can be canceled thanks to the optional cancellation <paramref name="token"/>, even if
        /// they cannot be awaited.
        /// </para>
        /// <para>
        /// This method returns false if and only if this device is destroyed at the time of its call: if the device is destroyed later, before
        /// the command is executed, the command will be silently ignored.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">Optional token that can cancel the command's execution.</param>
        /// <returns>True on success, false if this device is destroyed.</returns>
        public bool SendCommand( IActivityMonitor monitor, DeviceCommand command, CancellationToken token = default )
        {
            var error = DoEnqueue( new QueuedCommand( command, token ) );
            if( error != null )
            {
                monitor.Error( error.Message );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Must implement command handling without any result. Calls to this method and to <see cref="HandleCommandAsync{TResult}(IActivityMonitor, DeviceCommand{TResult}, CancellationToken)"/>
        /// are executed from the command loop managed by this <see cref="StdDevice{TConfiguration}"/>: calls are serialized, there should be no concurrency issue to handle.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">The token's cancellation toke.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task HandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token );

        /// <summary>
        /// Must implement command handling for commands with result. Calls to this method and to <see cref="HandleCommandAsync(IActivityMonitor, DeviceCommand, CancellationToken)"/>
        /// are executed from the command loop managed by this <see cref="StdDevice{TConfiguration}"/>: calls are serialized, there should be no concurrency issue to handle.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">The token's cancellation toke.</param>
        /// <returns>The command's result.</returns>
        protected abstract Task<TResult> HandleCommandAsync<TResult>( IActivityMonitor monitor, DeviceCommand<TResult> command, CancellationToken token );

        /// <summary>
        /// Extension point that is called for each command that must be executed while this device is stopped.
        /// This default implementation simply returns the defaultStoppedBehavior parameter value set in the constructor.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command that should be executed, deferred, canceled or set on error.</param>
        /// <returns>The behavior to apply for the command.</returns>
        protected virtual StoppedBehavior OnStoppedDeviceCommand( ActivityMonitor monitor, DeviceCommand command ) => _stoppedBehavior;

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
        protected virtual Task<bool> OnCommandErrorAsync( ActivityMonitor monitor, DeviceCommand command, Exception ex ) => Task.FromResult( true );


    }
}
