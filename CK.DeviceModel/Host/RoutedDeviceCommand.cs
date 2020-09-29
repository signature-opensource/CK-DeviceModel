using CK.Core;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Captures the call to execute for a <see cref="SyncDeviceCommand"/> or a <see cref="AsyncDeviceCommand"/> if a matching
    /// device has been found by <see cref="IDeviceHost.Handle(IActivityMonitor, DeviceCommand)"/>.
    /// </summary>
    public readonly struct RoutedDeviceCommand
    {
        readonly DeviceCommand _cmd;
        readonly IInternalDevice _device;

        internal RoutedDeviceCommand( DeviceCommand c, IInternalDevice d )
        {
            _cmd = c;
            _device = d;
        }

        /// <summary>
        /// Gets whether the command has found its executor.
        /// </summary>
        public bool Success => _cmd != null;

        /// <summary>
        /// Gets whether <see cref="ExecuteAsync(IActivityMonitor)"/> must be called or <see cref="Execute(IActivityMonitor)"/>.
        /// This is null if <see cref="Success"/> is false.
        /// </summary>
        public bool? IsAsync => _cmd == null ? (bool?)null : _cmd is AsyncDeviceCommand;

        /// <summary>
        /// Asynchronously executes the <see cref="AsyncDeviceCommand"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        public Task ExecuteAsync( IActivityMonitor monitor ) => _device.ExecuteAsync( monitor, (AsyncDeviceCommand)_cmd );

        /// <summary>
        /// Sychronously execute the <see cref="SyncDeviceCommand"/>.
        /// </summary>
        /// <param name="monitor"></param>
        public void Execute( IActivityMonitor monitor ) => _device.Execute( monitor, (SyncDeviceCommand)_cmd );
    }
}
