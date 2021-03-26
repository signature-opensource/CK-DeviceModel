using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base for <see cref="HostedStopDeviceCommand{THost}"/> command that
    /// stops a device.
    /// </summary>
    /// <remarks>
    /// This class cannot be specialized. The only concrete type of this command is <see cref="HostedStopDeviceCommand{THost}"/>.
    /// </remarks>
    public abstract class StopDeviceCommand : DeviceCommand<bool>
    {
        private protected StopDeviceCommand()
            : base( errorOrCancelResult: false )
        {
        }

        /// <summary>
        /// Gets or sets whether the <see cref="DeviceConfigurationStatus.AlwaysRunning"/> should be ignored.
        /// </summary>
        public bool IgnoreAlwaysRunning { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/> (will be a no-op) since it must obviously not be deferred until the next start.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    }

}
