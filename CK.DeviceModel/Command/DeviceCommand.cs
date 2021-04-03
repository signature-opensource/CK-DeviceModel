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

        /// <inheritdoc />
        public sealed override Type HostType => typeof(THost);

    }
}
