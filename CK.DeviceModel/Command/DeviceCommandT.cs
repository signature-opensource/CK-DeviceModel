using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for device commands that generates a result.
    /// This class cannot be directly specialized: the generic <see cref="HostedDeviceCommand{THost,TResult}"/> must be used.
    /// </summary>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    public abstract class DeviceCommand<TResult> : DeviceCommand
    {
    }
}
