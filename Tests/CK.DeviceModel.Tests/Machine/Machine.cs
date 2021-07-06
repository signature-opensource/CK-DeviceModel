using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

namespace CK.DeviceModel.Tests
{

    public class Machine : Device<MachineConfiguration>
    {
        public static int TotalCount;
        public static int TotalRunning;

        // A device can keep a reference to the current configuration:
        // this configuration is an independent clone that is accessible only to the Device.
        readonly MachineConfiguration _configRef;

        public Machine( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = info.Configuration;
        }

        /// <summary>
        /// Gets or sets whether <see cref="DoStartAsync(IActivityMonitor, DeviceStartedReason)"/> will fail.
        /// </summary>
        public bool FailToStart { get; set; }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, MachineConfiguration config )
        {
            return Task.FromResult( DeviceReconfiguredResult.None );
        }

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            Interlocked.Increment( ref TotalRunning );
            return Task.FromResult( !FailToStart );
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            Interlocked.Decrement( ref TotalRunning );
            return Task.CompletedTask;
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            return Task.CompletedTask;
        }

    }

}
