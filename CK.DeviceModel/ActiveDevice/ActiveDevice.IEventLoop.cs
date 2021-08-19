using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public partial class ActiveDevice<TConfiguration, TEvent>
    {
        /// <summary>
        /// Models the event loop API available inside an ActiveDevice.
        /// </summary>
        protected interface IEventLoop
        {
            /// <summary>
            /// Sends a device event into <see cref="DeviceEvent"/>.
            /// </summary>
            /// <param name="e">The event to send.</param>
            void RaiseEvent( TEvent e );

            /// <summary>
            /// Posts the given synchronous action to be executed on the event loop.
            /// </summary>
            /// <param name="action">The action to execute.</param>
            void Execute( Action<IActivityMonitor> action );

            /// <summary>
            /// Posts the given asynchronous action to be executed on the event loop.
            /// </summary>
            /// <param name="action">The action to execute.</param>
            void Execute( Func<IActivityMonitor, Task> action );

            /// <summary>
            /// Posts an error log message into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            void LogError( string msg );

            /// <summary>
            /// Posts an error log message with an exception into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            /// <param name="ex">The exception to log.</param>
            void LogError( string msg, Exception ex );

            /// <summary>
            /// Posts a warning log message into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            void LogWarn( string msg );

            /// <summary>
            /// Posts a warning log message with an exception into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            /// <param name="ex">The exception to log.</param>
            void LogWarn( string msg, Exception ex );

            /// <summary>
            /// Posts an informational message log into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            void LogInfo( string msg );

            /// <summary>
            /// Posts a trace log message into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            void LogTrace( string msg );

            /// <summary>
            /// Posts a debug log message into the event monitor.
            /// </summary>
            /// <param name="msg">The message to log.</param>
            void LogDebug( string msg );
        }
    }
}
