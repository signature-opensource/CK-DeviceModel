using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

namespace CK.DeviceModel.Tests
{
    public class Machine : Device<MachineConfiguration>, ITestDevice
    {
        public static int TotalCount;
        public static int TotalRunning;

        // A device can keep a reference to the current configuration:
        // this configuration is an independent clone that is accessible only to the Device.
        MachineConfiguration _configRef;

        public Machine( IActivityMonitor monitor, MachineConfiguration config )
            : base( monitor, config )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = config;
        }

        public Task TestAutoDestroyAsync( IActivityMonitor monitor ) => AutoDestroyAsync( monitor );

        public Task TestForceStopAsync( IActivityMonitor monitor ) => AutoStopAsync( monitor, ignoreAlwaysRunning: true );

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, MachineConfiguration config )
        {
            return Task.FromResult( DeviceReconfiguredResult.None );
        }

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            Interlocked.Increment( ref TotalRunning );
            return Task.FromResult( true );
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
