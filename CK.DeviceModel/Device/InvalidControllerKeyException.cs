using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel;

/// <summary>
/// Exception that is set on a command when the <see cref="IDevice.ControllerKey"/>
/// doesn't match the <see cref="BaseDeviceCommand.ControllerKey"/> and <see cref="IDevice.SendCommand"/>
/// has been used.
/// </summary>
public class InvalidControllerKeyException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="InvalidControllerKeyException"/>.
    /// </summary>
    /// <param name="message">The message.</param>
    public InvalidControllerKeyException( string message )
        : base( message )
    {
    }
}
