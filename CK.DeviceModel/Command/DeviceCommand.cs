using System;
using System.Collections.Generic;
using System.Text;
using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands without any result that a device can handle.
    /// </summary>
    public abstract class DeviceCommand<THost> : DeviceCommandNoResult where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost}"/>.
        /// </summary>
        protected DeviceCommand()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost}"/> thats ignores errors or cancellation.
        /// See <see cref="CommandCompletionSource"/>.
        /// </summary>
        /// <param name="ignoreException">True to ignore errors.</param>
        /// <param name="ignoreCanceled">True to ignore cancellation.</param>
        protected DeviceCommand( bool ignoreException, bool ignoreCanceled )
            : base( ignoreException, ignoreCanceled )
        {
        }

        /// <inheritdoc />
        public sealed override Type HostType => typeof(THost);

    }
}
