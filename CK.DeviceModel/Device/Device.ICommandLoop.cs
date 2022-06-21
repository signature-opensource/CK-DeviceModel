using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;

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
        protected interface ICommandLoop : IMonitoredWorker
        {
            /// <summary>
            /// Sends an immediate signal into the command loop that will be handled by <see cref="OnCommandSignalAsync(IActivityMonitor, object?)"/>.
            /// An <see cref="ArgumentException"/> is thrown if the <paramref name="payload"/> is a <see cref="BaseDeviceCommand"/>.
            /// </summary>
            /// <param name="payload">The payload to send. Must not be a command.</param>
            void Signal( object? payload );
        }

        sealed class LoopImpl : ICommandLoop
        {
            readonly ChannelWriter<object?> _writer;

            public LoopImpl( ChannelWriter<object?> w ) => _writer = w;

            void IMonitoredWorker.Execute( Action<IActivityMonitor> action ) => DoPost( action );
            void IMonitoredWorker.Execute( Func<IActivityMonitor, Task> action ) => DoPost( action );
            void IMonitoredWorker.LogError( string msg ) => DoPost( m => m.Error( msg ) );
            void IMonitoredWorker.LogError( string msg, Exception ex ) => DoPost( m => m.Error( msg, ex ) );
            void IMonitoredWorker.LogWarn( string msg ) => DoPost( m => m.Warn( msg ) );
            void IMonitoredWorker.LogWarn( string msg, Exception ex ) => DoPost( m => m.Warn( msg, ex ) );
            void IMonitoredWorker.LogInfo( string msg ) => DoPost( m => m.Info( msg ) );
            void IMonitoredWorker.LogTrace( string msg ) => DoPost( m => m.Trace( msg ) );
            void IMonitoredWorker.LogDebug( string msg ) => DoPost( m => m.Debug( msg ) );
            void ICommandLoop.Signal( object? payload ) => DoPost( payload );

            void DoPost( object? o )
            {
                Throw.CheckArgument( o is not BaseDeviceCommand );
                _writer.TryWrite( o );
            }
            void DoPost( Action<IActivityMonitor> o ) => _writer.TryWrite( o );
            void DoPost( Func<IActivityMonitor, Task> o ) => _writer.TryWrite( o );
        }

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
