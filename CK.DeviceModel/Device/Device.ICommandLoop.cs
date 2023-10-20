using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;
using System.Diagnostics;

namespace CK.DeviceModel
{
    public abstract partial class Device<TConfiguration>
    {
        /// <summary>
        /// Gets the command loop API that implementation can use to execute 
        /// actions, sends logs to the command loop or calls <see cref="ICommandLoop.Signal(object?)"/>.
        /// </summary>
        protected ICommandLoop CommandLoop => _commandLoop;

        /// <summary>
        /// The command loop exposed by <see cref="CommandLoop"/>.
        /// </summary>
        protected interface ICommandLoop
        {
            /// <summary>
            /// Gets the thread safe logger bound to this monitor's loop.
            /// </summary>
            IParallelLogger Logger { get; }

            /// <summary>
            /// Sends an immediate signal into the command loop that will be handled by <see cref="OnCommandSignalAsync(IActivityMonitor, object?)"/>.
            /// An <see cref="ArgumentException"/> is thrown if the <paramref name="payload"/> is a <see cref="BaseDeviceCommand"/>.
            /// </summary>
            /// <param name="payload">The payload to send. It must not be a command.</param>
            void Signal( object? payload );

            /// <summary>
            /// Executes a lambda function on the command loop. This is dangerous because
            /// of the closure lambda: fields may be written concurrently.
            /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
            /// (a record class should typically be used) to better express the "command" pattern.
            /// </summary>
            /// <param name="action">The action that will be executed in the command loop context.</param>
            void DangerousExecute( Action<IActivityMonitor> action );

            /// <summary>
            /// Executes an asynchronous lambda function on the command loop. This is dangerous because
            /// of the closure lambda: fields may be written concurrently.
            /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
            /// (a record class should typically be used) to better express the "command" pattern.
            /// </summary>
            /// <param name="action">The asynchronous action that will be executed in the command loop context.</param>
            void DangerousExecute( Func<IActivityMonitor, Task> action );
        }

        sealed class LoopImpl : ICommandLoop
        {
            readonly IParallelLogger _commandLogger;
            readonly ChannelWriter<BaseDeviceCommand> _queue;
            readonly ChannelWriter<object?> _immediate;

            public LoopImpl( IParallelLogger commandLogger, ChannelWriter<BaseDeviceCommand> queue, ChannelWriter<object?> immediate )
            {
                _commandLogger = commandLogger;
                _queue = queue;
                _immediate = immediate;
            }

            IParallelLogger ICommandLoop.Logger => _commandLogger;

            void ICommandLoop.DangerousExecute( Action<IActivityMonitor> action ) => DoPost( action );

            void ICommandLoop.DangerousExecute( Func<IActivityMonitor, Task> action ) => DoPost( action );

            void ICommandLoop.Signal( object? payload ) => DoPost( payload );

            void DoPost( object? o )
            {
                Throw.CheckArgument( o is not BaseDeviceCommand );
                if( _immediate.TryWrite( o ) ) _queue.TryWrite( CommandAwaker.Instance );
            }
            void DoPost( Action<IActivityMonitor> o ) => _immediate.TryWrite( o );
            void DoPost( Func<IActivityMonitor, Task> o ) => _immediate.TryWrite( o );
        }

        /// <summary>
        /// Gets whether the current activity is executing in the command loop.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <returns>True if the monitor is the command loop monitor, false otherwise.</returns>
        protected bool IsInCommandLoop( IActivityMonitor monitor ) => monitor.Output == _commandMonitor.Output;

        /// <summary>
        /// Optional extension point that must handle <see cref="ICommandLoop.Signal(object?)"/> payloads.
        /// This does nothing at this level.
        /// <para>
        /// Any exceptions raised by this method will stop the device.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="payload">The signal payload.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnCommandSignalAsync( IActivityMonitor monitor, object? payload ) => Task.CompletedTask;
    }
}
