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
        readonly Channel<CompletionCommandBase> _commandQueue;
        readonly Queue<CompletionCommandBase> _deferredCommands;

        readonly StoppedBehavior _stoppedBehavior;

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
            _commandQueue = Channel.CreateUnbounded<CompletionCommandBase>( new UnboundedChannelOptions() { SingleReader = true } );
            _deferredCommands = new Queue<CompletionCommandBase>();
            _stoppedBehavior = defaultStoppedBehavior;
            _ = Task.Run( RunLoop );
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
            var error = DoEnqueue( new CompletionCommandBase( command, token ) );
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
        /// <para>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">The token's cancellation toke.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task HandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token )
        {
            throw new ArgumentException( $"Unhandled command {command.GetType().Name}.", nameof( command ) );
        }

        /// <summary>
        /// Must implement command handling for commands with result. Calls to this method and to <see cref="HandleCommandAsync(IActivityMonitor, DeviceCommand, CancellationToken)"/>
        /// are executed from the command loop managed by this <see cref="StdDevice{TConfiguration}"/>: calls are serialized, there should be no concurrency issue to handle.
        /// <para>
        /// Since all commands should be handled, this default implementation systematically throws a <see cref="ArgumentException"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="token">The token's cancellation toke.</param>
        /// <returns>The command's result.</returns>
        protected virtual Task<TResult> HandleCommandAsync<TResult>( IActivityMonitor monitor, DeviceCommand<TResult> command, CancellationToken token )
        {
            throw new ArgumentException( $"Unhandled command {command.GetType().Name}.", nameof( command ) );
        }


    }
}
