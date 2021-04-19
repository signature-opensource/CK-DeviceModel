using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Single command that supports the basic device operations.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public class BasicControlDeviceCommand<THost> : BasicControlDeviceCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="BasicControlDeviceCommand"/> with a <see cref="BasicControlDeviceCommand.Operation"/>
        /// set to <see cref="BasicControlDeviceOperation.None"/>.
        /// </summary>
        public BasicControlDeviceCommand()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="BasicControlDeviceCommand"/>.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        public BasicControlDeviceCommand( BasicControlDeviceOperation operation )
        {
            Operation = operation;
        }

        /// <inheritdoc />
        public override Type HostType => typeof( THost );
    }

}
