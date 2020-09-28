using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract, non generic, base class for standard commands that a device can handle synchronously.
    /// This class cannot be directly specialized: the generic <see cref="SyncDeviceCommand{THost}"/>
    /// must be used.
    /// </summary>
    public abstract class SyncDeviceCommand : DeviceCommand
    {
        private protected SyncDeviceCommand() { }

    }
}
