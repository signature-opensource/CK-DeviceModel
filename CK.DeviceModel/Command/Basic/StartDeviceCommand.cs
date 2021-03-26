using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="HostedStartDeviceCommand{THost}"/> command that
    /// attempts to start a device.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="HostedStartDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class StartDeviceCommand : DeviceCommand<bool>
    {
        private protected StartDeviceCommand()
            : base( errorOrCancelResult: false )
        {
        }

        /// <summary>
        /// Obviously returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    }

}
