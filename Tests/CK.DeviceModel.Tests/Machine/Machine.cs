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
        readonly MachineConfiguration _configRef;

        public Machine( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = info.Configuration;
        }

        public CommandCompletion SendAutoDestroy( IActivityMonitor monitor )
        {
            var cmd = new AutoDestroyCommand<OtherMachineHost>() { DeviceName = Name, ControllerKey = ControllerKey };
            SendCommand( monitor, cmd );
            return cmd.Result;
        }

        public CommandCompletion<bool> SendForceAutoStop( IActivityMonitor monitor )
        {
            var cmd = new ForceAutoStopCommand<OtherMachineHost>() { DeviceName = Name, ControllerKey = ControllerKey };
            SendCommand( monitor, cmd );
            return cmd.Result;
        }

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

        protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token )
        {
            if( command is ForceAutoStopCommand<MachineHost> autoStop )
            {
                autoStop.Result.SetSuccess( await AutoStopAsync( monitor, true ) );
            }
            else if( command is AutoDestroyCommand<MachineHost> autoDestroy )
            {
                await AutoDestroyAsync( monitor );
                autoDestroy.Result.SetSuccess();
            }
            else
            {
                await base.DoHandleCommandAsync( monitor, command, token );
            }
        }
    }

}
