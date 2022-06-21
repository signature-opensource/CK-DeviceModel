using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Command to stop a device.
    /// This command is by default (like the other basic commands), sent immediately (<see cref="BaseDeviceCommand.ImmediateSending"/> is true).
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public sealed class StopDeviceCommand<THost> : BaseStopDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="StopDeviceCommand{THost}"/>.
        /// </summary>
        public StopDeviceCommand()
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
