using System.Diagnostics;

namespace CK.DeviceModel;

/// <summary>
/// Event raised whenever a device's configuration changed.
/// </summary>
public sealed class DeviceLifetimeEvent<TConfiguration> : DeviceLifetimeEvent
    where TConfiguration : DeviceConfiguration, new()
{
    internal DeviceLifetimeEvent( IDevice device, int sequenceNumber, bool status, bool configuration, bool controllerKey )
        : base( device, sequenceNumber, status, configuration, controllerKey )
    {
        Debug.Assert( status || configuration || controllerKey );
    }

    /// <summary>
    /// Gets the device configuration.
    /// </summary>
    public new TConfiguration Configuration => (TConfiguration)base.Configuration;
}
