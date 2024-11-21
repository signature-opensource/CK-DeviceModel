using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel;

/// <summary>
/// Base class for <see cref="DeviceLifetimeEvent"/> and all <see cref="ActiveDeviceEvent{TDevice}"/>.
/// </summary>
public abstract class BaseDeviceEvent
{
    /// <summary>
    /// Initializes a new <see cref="BaseDeviceEvent"/> with its originating device.
    /// </summary>
    /// <param name="device">The device that raised this event.</param>
    protected BaseDeviceEvent( IDevice device )
    {
        Throw.CheckNotNullArgument( device );
        Device = device;
    }

    /// <summary>
    /// Gets the device that raised this event.
    /// </summary>
    public IDevice Device { get; }
}
