using CK.Core;
using System;
using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Defines the action that must be taken for commands that are handled when the device is stopped.
    /// The default behavior of a device can be configured thanks to <see cref="DeviceConfiguration.DefaultStoppedBehavior"/>
    /// but this default behavior may be altered for any command by overriding the <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, DeviceCommandBase)"/>
    /// protected method.
    /// </summary>
    public enum DeviceCommandStoppedBehavior
    {
        /// <summary>
        /// A <see cref="DeviceStoppedException"/> is set on the <see cref="DeviceCommand.Result"/> or <see cref="DeviceCommand{TResult}.Result"/>.
        /// </summary>
        SetDeviceStoppedException,

        /// <summary>
        /// A <see cref="OperationCanceledException"/> is set on the <see cref="DeviceCommand.Result"/> or <see cref="DeviceCommand{TResult}.Result"/>.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The command will be executed (<see cref="HandleCommandAsync(IActivityMonitor, DeviceCommandBase, CancellationToken)"/>
        /// will be called) regardless of the stopped status.
        /// </summary>
        Handle,

        /// <summary>
        /// The command is stored in an internal queue and as soon as the device is started, the command
        /// is executed. This enables commands to be resilient to temporary (unexpected) stops of the device and should be typically
        /// used in conjunction with <see cref="DeviceConfigurationStatus.AlwaysRunning"/>.
        /// </summary>
        WaitForNextStart
    }

}
