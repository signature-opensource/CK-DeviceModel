using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic event loop specialized by <see cref="ActiveDevice{TConfiguration, TEvent}.IEventLoop"/>.
    /// </summary>
    public interface IEventLoop : IActivityLogger
    {
        /// <summary>
        /// Sends an immediate signal into the event loop that will be handled by <see cref="ActiveDevice.OnEventSignalAsync(IActivityMonitor, object?)"/>.
        /// An <see cref="ArgumentException"/> is thrown if the <paramref name="payload"/> is a <see cref="BaseDeviceCommand"/>
        /// or <see cref="BaseDeviceEvent"/>.
        /// </summary>
        /// <param name="payload">The payload to send. It must not be a command nor an event.</param>
        void Signal( object? payload );

        /// <summary>
        /// Executes a lambda function on the event loop. This is dangerous because
        /// of the closure lambda: fields may be written concurrently.
        /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
        /// (a record class should typically be used) to better express the "command" pattern.
        /// </summary>
        /// <param name="action">The action that will be executed in the command loop context.</param>
        void DangerousExecute( Action<IActivityMonitor> action );

        /// <summary>
        /// Executes an asynchronous lambda function on the event loop. This is dangerous because
        /// of the closure lambda: fields may be written concurrently.
        /// It is safer to use the <see cref="Signal(object?)"/> with an explicit payload
        /// (a record class should typically be used) to better express the "command" pattern.
        /// </summary>
        /// <param name="action">The asynchronous action that will be executed in the command loop context.</param>
        void DangerousExecute( Func<IActivityMonitor, Task> action );

    }
}
