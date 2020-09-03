using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines non-generic device host properties.
    /// </summary>
    public interface IDeviceHost
    {
        /// <summary>
        /// Gets the host name that SHOULD identify this host instance unambiguously in a running context.
        /// </summary>
        string DeviceHostName { get; }

        /// <summary>
        /// Gets the number of devices.
        /// </summary>
        int Count { get; }
    }
}
