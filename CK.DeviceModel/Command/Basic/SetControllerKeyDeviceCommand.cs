using System;

namespace CK.DeviceModel;

/// <summary>
/// Command to set a new <see cref="IDevice.ControllerKey"/>.
/// This command is by default (like the other basic commands), sent immediately (<see cref="BaseDeviceCommand.ImmediateSending"/> is true).
/// </summary>
/// <typeparam name="THost">The device host type.</typeparam>
public sealed class SetControllerKeyDeviceCommand<THost> : BaseSetControllerKeyDeviceCommand where THost : IDeviceHost
{
    /// <summary>
    /// Initializes a new <see cref="SetControllerKeyDeviceCommand{THost}"/>.
    /// </summary>
    public SetControllerKeyDeviceCommand()
    {
    }

    /// <inheritdoc />
    public override Type HostType => typeof( THost );
}
