using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    public class StdDeviceHost<T, THostConfiguration, TConfiguration> : DeviceHost<T, THostConfiguration, TConfiguration>
        where T : StdDevice<TConfiguration>
        where THostConfiguration : DeviceHostConfiguration<TConfiguration>
        where TConfiguration : StdDeviceConfiguration
    {

        /// <summary>
        /// Initializes a new host.
        /// </summary>
        /// <param name="deviceHostName">A name that SHOULD identify this host instance unambiguously in a running context.</param>
        /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
        protected StdDeviceHost( string deviceHostName, IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( deviceHostName, alwaysRunningPolicy )
        {
        }

        /// <summary>
        /// Initializes a new host with a <see cref="DeviceHostName"/> sets to its type name.
        /// </summary>
        /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
        protected StdDeviceHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }

    }
}
