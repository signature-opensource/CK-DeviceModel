using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

namespace CK.DeviceModel.Tests
{
    public class Camera : Device<CameraConfiguration>
    {
        public static int TotalCount;
        public static int TotalRunning;

        // A device can keep a reference to the current configuration:
        // this configuration is an independent clone that is accessible only to the Device.
        CameraConfiguration _configRef;
        readonly PerfectEventSender<Camera,int> _flash;

        public Camera( IActivityMonitor monitor, CameraConfiguration config )
            : base( monitor, config )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = config;
            _flash = new PerfectEventSender<Camera,int>();
        }

        public PerfectEvent<Camera,int> Flash => _flash.PerfectEvent;

        public Task TestAutoDestroy( IActivityMonitor monitor ) => AutoDestroyAsync( monitor );

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

        protected override Task DoHandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand command )
        {
            if( command is FlashCommand )
            {
                return _flash.RaiseAsync( monitor, this, _configRef.FlashColor );
            }
            return base.DoHandleCommandAsync( monitor, command );
        }

        protected override void DoHandleCommand( IActivityMonitor monitor, SyncDeviceCommand command )
        {
            if( command is SetFlashColorCommand f )
            {
                _configRef.FlashColor = f.Color;
                return;
            }
            base.DoHandleCommand( monitor, command );
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            return Task.CompletedTask;
        }
    }

}
