using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

namespace CK.DeviceModel.Tests
{
    public class Camera : Device<CameraConfiguration>, ITestDevice
    {
        public static int TotalCount;
        public static int TotalRunning;

        // A device can keep a reference to the current configuration:
        // this configuration is an independent clone that is accessible only to the Device.
        CameraConfiguration _configRef;
        readonly PerfectEventSender<Camera,int> _flash;

        public Camera( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = info.Configuration;
            _flash = new PerfectEventSender<Camera,int>();
        }

        public PerfectEvent<Camera,int> Flash => _flash.PerfectEvent;

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

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config )
        {
            bool configHasChanged = config.FlashColor != _configRef.FlashColor;
            _configRef = config;
            return Task.FromResult( configHasChanged ? DeviceReconfiguredResult.UpdateSucceeded : DeviceReconfiguredResult.None );
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

        protected override Task DoHandleCommandAsync( IActivityMonitor monitor, DeviceCommandBase command, CancellationToken token )
        {
            if( command is FlashCommand )
            {
                return _flash.RaiseAsync( monitor, this, _configRef.FlashColor );
            }
            if( command is SetFlashColorCommand f )
            {
                _configRef.FlashColor = f.Color;
                return Task.CompletedTask;
            }
            return base.DoHandleCommandAsync( monitor, command, token );
        }


        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            _flash.RemoveAll();
            return Task.CompletedTask;
        }
    }

}
