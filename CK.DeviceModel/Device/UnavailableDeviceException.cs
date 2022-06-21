using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Exception that can be raised when a device must handle a command while stopped 
    /// (see <see cref="DeviceCommandStoppedBehavior"/>) or when the device has been destroyed.
    /// </summary>
    public class UnavailableDeviceException : Exception
    {
        /// <summary>
        /// Initializes a new <see cref="UnavailableDeviceException"/> for command on a device.
        /// </summary>
        /// <param name="device">The stopped or destroyed device.</param>
        /// <param name="command">The command.</param>
        public UnavailableDeviceException( IDevice device, BaseDeviceCommand command )
            : base( $"Unable to execute command '{command}' on device '{device?.FullName}', its status is {device?.Status}." )
        {
        }
    }
}
