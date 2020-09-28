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
        readonly PerfectEventSender<Camera> _flash;

        public Camera( IActivityMonitor monitor, CameraConfiguration config )
            : base( monitor, config )
        {
            Interlocked.Increment( ref TotalCount );
            _configRef = config;
            _flash = new PerfectEventSender<Camera>();
        }

        public PerfectEvent<Camera> Flash => _flash.PerfectEvent;

        public Task TestAutoDestroy( IActivityMonitor monitor ) => AutoDestroyAsync( monitor );

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CameraConfiguration config, bool controllerKeyChanged )
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

        protected override async Task<bool> DoHandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand commmand )
        {
            if( commmand is FlashCommand )
            {
                await _flash.RaiseAsync( monitor, this );
            }
            return false;
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            return Task.CompletedTask;
        }
    }

}
