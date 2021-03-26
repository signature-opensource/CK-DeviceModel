using System;
using System.Collections.Generic;
using System.Text;
using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands without any result that a device can handle.
    /// </summary>
    public abstract class HostedDeviceCommand<THost> : DeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="HostedDeviceCommand{THost}"/>.
        /// </summary>
        protected HostedDeviceCommand()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="HostedDeviceCommand{THost}"/> thats ignores errors or cancellation.
        /// See <see cref="CommandCompletionSource"/>.
        /// </summary>
        /// <param name="ignoreException">True to ignore errors.</param>
        /// <param name="ignoreCanceled">True to ignore cancellation.</param>
        protected HostedDeviceCommand( bool ignoreException, bool ignoreCanceled )
            : base( ignoreException, ignoreCanceled )
        {
        }

        /// <inheritdoc />
        public override Type HostType => typeof(THost);

    }
}
