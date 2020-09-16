using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Details of lifetime events exposed by a <see cref="IDevice"/>.
    /// </summary>
    public readonly struct DeviceStateChangedEvent
    {
        readonly int _status;

        /// <summary>
        /// Gets whether the device has started.
        /// </summary>
        public bool IsStartedEvent { get; }

        /// <summary>
        /// Gets whether the device has stopped.
        /// </summary>
        public bool IsStoppedEvent { get; }

        /// <summary>
        /// Gets whether the device has been reconfigured.
        /// </summary>
        public bool IsReconfiguredEvent { get; }

        /// <summary>
        /// Gets the <see cref="DeviceReconfiguredResult"/> if <see cref="IsReconfiguredEvent"/> is true (<see cref="DeviceReconfiguredResult.None"/> otherwise).
        /// </summary>
        public DeviceReconfiguredResult ReconfiguredResult => IsReconfiguredEvent ? (DeviceReconfiguredResult)_status : DeviceReconfiguredResult.None;

        /// <summary>
        /// Gets the <see cref="DeviceStartedReason"/> if <see cref="IsStartedEvent"/> is true (<see cref="DeviceStartedReason.None"/> otherwise).
        /// </summary>
        public DeviceStartedReason StartedReason => IsStartedEvent ? (DeviceStartedReason)_status : DeviceStartedReason.None;

        /// <summary>
        /// Gets the <see cref="DeviceStoppedReason"/> if <see cref="IsStoppedEvent"/> is true (<see cref="DeviceStoppedReason.None"/> otherwise).
        /// </summary>
        public DeviceStoppedReason StoppedReason => IsStoppedEvent ? (DeviceStoppedReason)_status : DeviceStoppedReason.None;

        /// <summary>
        /// Returns the appropriate enum string.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString()
        {
            return IsStartedEvent
                    ? StartedReason.ToString()
                    : (IsStoppedEvent
                        ? StoppedReason.ToString()
                        : ReconfiguredResult.ToString());
        }

        internal DeviceStateChangedEvent( DeviceReconfiguredResult r )
        {
            _status = (int)r;
            IsReconfiguredEvent = true;
            IsStartedEvent = false;
            IsStoppedEvent = false;
        }

        internal DeviceStateChangedEvent( DeviceStartedReason r )
        {
            _status = (int)r;
            IsReconfiguredEvent = false;
            IsStartedEvent = true;
            IsStoppedEvent = false;
        }

        internal DeviceStateChangedEvent( DeviceStoppedReason r )
        {
            _status = (int)r;
            IsReconfiguredEvent = false;
            IsStartedEvent = false;
            IsStoppedEvent = true;
        }

    }
}
