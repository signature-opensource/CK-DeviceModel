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

        /// <summary>
        /// Determines whether the <see cref="DeviceCommand.HostType"/> is compatible with the actual type of this host,
        /// finds the target device based on <see cref="DeviceCommand.DeviceName"/>, checks the <see cref="DeviceCommand.ControllerKey"/>
        /// against the <see cref="IDevice.ControllerKey"/> and calls <see cref="DeviceCommand.CheckValidity(IActivityMonitor)"/> before
        /// sending the command to the device.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to validate, route and execute.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>The <see cref="StdDeviceHostSendCommandResult"/>.</returns>
        public StdDeviceHostSendCommandResult SendCommand( IActivityMonitor monitor, DeviceCommand command, CancellationToken token = default )
        {
            var (status, device) = RouteCommand( monitor, command );
            if( status != DeviceHostCommandResult.Success ) return (StdDeviceHostSendCommandResult)status;
            Debug.Assert( device != null );
            if( !device.SendCommand( monitor, command, token ) )
            {
                return StdDeviceHostSendCommandResult.SendCommandError;
            }
            return StdDeviceHostSendCommandResult.Success;
        }

    }
}
