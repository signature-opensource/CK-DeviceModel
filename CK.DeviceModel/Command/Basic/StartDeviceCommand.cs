using System;

namespace CK.DeviceModel;

/// <summary>
/// Command to start a device.
/// This command is by default (like the other basic commands), sent immediately (<see cref="BaseDeviceCommand.ImmediateSending"/> is true).
/// </summary>
/// <typeparam name="THost">The device host type.</typeparam>
public sealed class StartDeviceCommand<THost> : BaseStartDeviceCommand where THost : IDeviceHost
{
    /// <summary>
    /// Initializes a new <see cref="StartDeviceCommand{THost}"/>.
    /// </summary>
    public StartDeviceCommand()
    {
    }

    /// <inheritdoc />
    public override Type HostType => typeof( THost );
}
