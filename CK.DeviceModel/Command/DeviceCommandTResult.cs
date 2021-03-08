using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for device commands that generates a result.
    /// </summary>
    /// <typeparam name="THost">The type of the <see cref="IDeviceHost"/>.</typeparam>
    /// <typeparam name="TResult">The type of the command's result.</typeparam>
    public abstract class DeviceCommand<THost,TResult> : DeviceCommand<THost> where THost : IDeviceHost
    {
    }
}
