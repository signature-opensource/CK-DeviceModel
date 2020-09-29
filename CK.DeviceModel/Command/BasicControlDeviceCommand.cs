using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Command
{
    /// <summary>
    /// Non generic base for <see cref="BasicControlDeviceCommand{THost}"/> command that
    /// supports the basic device operations.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="BasicControlDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class BasicControlDeviceCommand : AsyncDeviceCommand
    {
        private protected BasicControlDeviceCommand() { }

        /// <summary>
        /// Gets or sets what must be done.
        /// </summary>
        public BasicControlDeviceOperation Operation { get; set; }
    }

}
