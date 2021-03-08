using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for device commands that expects a result.
    /// </summary>
    /// <typeparam name="THost">The type of the <see cref="IDeviceHost"/>.</typeparam>
    /// <typeparam name="TResult">The type of the expected result.</typeparam>
    public abstract class SyncDeviceCommand<THost,TResult> : SyncDeviceCommand<THost> where THost : IDeviceHost
    {
    }
}
