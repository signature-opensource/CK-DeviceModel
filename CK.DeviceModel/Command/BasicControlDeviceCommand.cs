using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Command
{
    /// <summary>
    /// Single command that supports the basic device operations.
    /// </summary>
    /// <typeparam name="THost">The device host type.</typeparam>
    public partial class BasicControlDeviceCommand<THost> : AsyncDeviceCommand<THost>, IBasicCommand where THost : IDeviceHost
    {
        /// <summary>
        /// Gets or sets what must be done.
        /// </summary>
        public BasicControlDeviceOperation Operation { get; set; }
    }

    internal interface IBasicCommand
    {
        BasicControlDeviceOperation Operation { get; }
    }
}
