using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for standard commands that a device can handle asynchronously.
    /// This class cannot be directly specialized: the generic <see cref="AsyncDeviceCommand{THost}"/>
    /// must be used.
    /// </summary>
    public abstract class AsyncDeviceCommand : DeviceCommand
    {
        private protected AsyncDeviceCommand() { }

    }
}
