using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace CK.DeviceModel;

/// <summary>
/// Unifies <see cref="IDevice"/>'s lifetime status: this captures the last change that occurred
/// and whether it is currently <see cref="IsRunning"/> or <see cref="IsDestroyed"/>.
/// </summary>
public readonly struct DeviceStatus : IEquatable<DeviceStatus>
{
    readonly int _status;
    readonly byte _last;
    readonly bool _running;
    readonly bool _systemShutdown;

    /// <summary>
    /// Gets whether the device has started.
    /// </summary>
    public bool HasStarted => _last == 1;

    /// <summary>
    /// Gets whether the device has stopped.
    /// </summary>
    public bool HasStopped => _last == 2;

    /// <summary>
    /// Gets whether the device has been reconfigured.
    /// </summary>
    public bool HasBeenReconfigured => _last == 3;

    /// <summary>
    /// Gets whether the device is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Gets whether the system is currently shutting down
    /// (the <see cref="DeviceHostDaemon.StoppedToken"/> has been signaled).
    /// </summary>
    /// <remarks>
    /// This property can be used to skip any further process. It is for informations
    /// and is not considered when testing whether the status has changed (it is ignored
    /// by <see cref="Equals(DeviceStatus)"/>).
    /// </remarks>
    public bool IsSystemShutdown => _systemShutdown;

    /// <summary>
    /// Gets whether the device has been destroyed.
    /// </summary>
    public bool IsDestroyed => HasStopped && (StoppedReason == DeviceStoppedReason.Destroyed || StoppedReason == DeviceStoppedReason.SelfDestroyed);

    /// <summary>
    /// Gets the <see cref="DeviceReconfiguredResult"/> if <see cref="HasBeenReconfigured"/> is true (<see cref="DeviceReconfiguredResult.None"/> otherwise).
    /// </summary>
    public DeviceReconfiguredResult ReconfiguredResult => HasBeenReconfigured ? (DeviceReconfiguredResult)_status : DeviceReconfiguredResult.None;

    /// <summary>
    /// Gets the <see cref="DeviceStartedReason"/> if <see cref="HasStarted"/> is true (<see cref="DeviceStartedReason.None"/> otherwise).
    /// </summary>
    public DeviceStartedReason StartedReason => HasStarted ? (DeviceStartedReason)_status : DeviceStartedReason.None;

    /// <summary>
    /// Gets the <see cref="DeviceStoppedReason"/> if <see cref="HasStopped"/> is true (<see cref="DeviceStoppedReason.None"/> otherwise).
    /// </summary>
    public DeviceStoppedReason StoppedReason => HasStopped ? (DeviceStoppedReason)_status : DeviceStoppedReason.None;

    /// <summary>
    /// Returns "Running" or "Stopped" followed by the appropriate enum reason string inside parentheses.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString()
    {
        return (IsRunning ? "Running (" : "Stopped (")
                + (HasStarted
                    ? StartedReason.ToString()
                    : (HasStopped
                        ? StoppedReason.ToString()
                        : ReconfiguredResult.ToString())) + ")";
    }

    /// <summary>
    /// Implements simple value equality.
    /// </summary>
    /// <param name="other">The other status.</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public bool Equals( DeviceStatus other )
    {
        return _status == other._status
                && _last == other._last
                && _running == other._running;
    }

    /// <summary>
    /// Simple relay to <see cref="Equals(DeviceStatus)"/>.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public override bool Equals( object? obj ) => obj is DeviceStatus s && Equals( s );

    /// <summary>
    /// Computes hash based on value equality. 
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => HashCode.Combine( _status, _last, _running );

#pragma warning disable 1591
    public static bool operator ==( DeviceStatus s1, DeviceStatus s2 ) => s1.Equals( s2 );
    public static bool operator !=( DeviceStatus s1, DeviceStatus s2 ) => !s1.Equals( s2 );
#pragma warning restore 1591

    internal DeviceStatus( DeviceReconfiguredResult r, bool isRunning, bool systemShutdown )
    {
        _status = (int)r;
        _last = 3;
        _running = isRunning;
        _systemShutdown = systemShutdown;
        Debug.Assert( !HasStarted && !HasStopped && HasBeenReconfigured );
    }

    internal DeviceStatus( DeviceStartedReason r, bool systemShutdown )
    {
        _status = (int)r;
        _last = 1;
        _running = true;
        _systemShutdown = systemShutdown;
        Debug.Assert( HasStarted && !HasStopped && !HasBeenReconfigured );
    }

    internal DeviceStatus( DeviceStoppedReason r, bool systemShutdown )
    {
        _status = (int)r;
        _last = 2;
        _running = false;
        _systemShutdown = systemShutdown;
        Debug.Assert( !HasStarted && HasStopped && !HasBeenReconfigured );
    }

}
