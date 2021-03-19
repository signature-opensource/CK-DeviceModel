using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Exception that can be raised when a device must handle a command while stopped.
    /// See <see cref="DeviceCommandStoppedBehavior"/>.
    /// </summary>
    public class DeviceStoppedException : Exception
    {
        /// <summary>
        /// Initializes a new <see cref="DeviceStoppedException"/> for command on a device.
        /// </summary>
        /// <param name="device">The stopped device.</param>
        /// <param name="command">The command.</param>
        public DeviceStoppedException( IDevice device, DeviceCommandBase command )
            : base( $"Unable to execute command '{command?.GetType().Name}' on device '{device?.FullName}', its status is {device?.Status}." )
        {
        }
    }
}
