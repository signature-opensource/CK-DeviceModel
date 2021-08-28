using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands with result that a device can handle.
    /// </summary>
    public abstract class DeviceCommand<THost,TResult> : DeviceCommandWithResult<TResult> where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost,TResult}"/>.
        /// </summary>
        protected DeviceCommand()
        {
        }

        /// <inheritdoc />
        public sealed override Type HostType => typeof(THost);
    }
}
