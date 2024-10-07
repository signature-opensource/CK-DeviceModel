using System.Threading;

namespace CK.DeviceModel;

/// <summary>
/// Non generic base type for <see cref="Device{TConfiguration}"/>.
/// </summary>
public class BaseDevice
{
    // Unsigned integer for infinite.
    internal const uint _unsignedTimeoutInfinite = unchecked((uint)Timeout.Infinite);
    // Tick resolution (used by Timer): 15 milliseconds.
    internal const long _tickCountResolution = 15;
    // Maximal TimeSpan in ticks.
    internal const long _ourMax = uint.MaxValue - 2 * _tickCountResolution;

    /// <summary>
    /// Gets the maximum number of pooled reminder that a given device can use.
    /// When a device needs more reminders (at the same time), those reminders are not pooled.
    /// </summary>
    public static int ReminderMaxPooledPerDevice => ReminderPool._maxPooledReminderPerDevice;

    /// <summary>
    /// Gets the current number of pooled reminder that are being used (out of the pool).
    /// </summary>
    public static int ReminderPoolInUseCount => ReminderPool._inUsePooledReminder;

    /// <summary>
    /// Gets the total number of pooled reminders.
    /// </summary>
    public static int ReminderPoolTotalCount => ReminderPool._totalPooledReminder;

}
