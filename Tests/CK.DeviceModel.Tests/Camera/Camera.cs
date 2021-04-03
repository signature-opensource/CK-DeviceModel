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
        // Here we use the 
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

        public async Task<bool> FlashAsync( IActivityMonitor monitor )
        {
            var cmd = new FlashCommand();
            if( !UnsafeSendCommand( monitor, cmd ) )
            {
                // The device is destroyed.
                return false;
            }
            // Wait for the command to complete.
            await cmd.Completion.Task;
            return true;
        }

        protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
        {
            if( command is FlashCommand f )
            {
                await _flash.RaiseAsync( monitor, this, _configRef.FlashColor ).ConfigureAwait( false );
                f.Completion.SetResult();
                return;
            }
            if( command is SetFlashColorCommand c )
            {
                _configRef.FlashColor = c.Color;
                c.Completion.SetResult();
                return;
            }
            await base.DoHandleCommandAsync( monitor, command, token );
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            _flash.RemoveAll();
            return Task.CompletedTask;
        }
    }

}
