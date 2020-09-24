using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Unifies lifetime status exposed of a <see cref="IDevice"/>: this captures
    /// the last change that occurred.
    /// </summary>
    public readonly struct DeviceStatus
    {
        readonly int _status;

        /// <summary>
        /// Gets whether the device has started.
        /// </summary>
        public bool IsStarted { get; }

        /// <summary>
        /// Gets whether the device has stopped.
        /// </summary>
        public bool IsStopped { get; }

        /// <summary>
        /// Gets whether the device has been reconfigured.
        /// </summary>
        public bool IsReconfigured { get; }

        /// <summary>
        /// Gets whether the device is currently running.
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// Gets the <see cref="DeviceReconfiguredResult"/> if <see cref="IsReconfigured"/> is true (<see cref="DeviceReconfiguredResult.None"/> otherwise).
        /// </summary>
        public DeviceReconfiguredResult ReconfiguredResult => IsReconfigured ? (DeviceReconfiguredResult)_status : DeviceReconfiguredResult.None;

        /// <summary>
        /// Gets the <see cref="DeviceStartedReason"/> if <see cref="IsStarted"/> is true (<see cref="DeviceStartedReason.None"/> otherwise).
        /// </summary>
        public DeviceStartedReason StartedReason => IsStarted ? (DeviceStartedReason)_status : DeviceStartedReason.None;

        /// <summary>
        /// Gets the <see cref="DeviceStoppedReason"/> if <see cref="IsStopped"/> is true (<see cref="DeviceStoppedReason.None"/> otherwise).
        /// </summary>
        public DeviceStoppedReason StoppedReason => IsStopped ? (DeviceStoppedReason)_status : DeviceStoppedReason.None;

        /// <summary>
        /// Returns "Running" or "Stopped" followed by the appropriate enum string in parentheses.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString()
        {
            return (IsRunning ? "Running (" : "Stopped (" )
                    + (IsStarted
                        ? StartedReason.ToString()
                        : (IsStopped
                            ? StoppedReason.ToString()
                            : ReconfiguredResult.ToString()));
        }

        internal DeviceStatus( DeviceReconfiguredResult r, bool isRunning )
        {
            _status = (int)r;
            IsReconfigured = true;
            IsStarted = false;
            IsStopped = false;
            IsRunning = isRunning;
        }

        internal DeviceStatus( DeviceStartedReason r )
        {
            _status = (int)r;
            IsReconfigured = false;
            IsStarted = true;
            IsStopped = false;
            IsRunning = true;
        }

        internal DeviceStatus( DeviceStoppedReason r )
        {
            _status = (int)r;
            IsReconfigured = false;
            IsStarted = false;
            IsStopped = true;
            IsRunning = false;
        }

    }
}
