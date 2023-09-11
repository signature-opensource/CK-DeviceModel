using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.PerfectEvent;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace CK.DeviceModel.Tests
{
    public class FlashBulb : Device<FlashBulbConfiguration>
    {
        public static int TotalCount;
        public static int TotalRunning;
        public static int OnCommandComplededCount;

        int _color;
        bool _colorFromConfig;


        // For test, not for doc.
        readonly PerfectEventSender<IDevice, int> _testFlash;
        public PerfectEvent<IDevice, int> TestFlash => _testFlash.PerfectEvent;

        public FlashBulb( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            _color = info.Configuration.FlashColor;
            _colorFromConfig = true;
            Interlocked.Increment( ref TotalCount );
            _testFlash = new PerfectEventSender<IDevice, int>();
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor,
                                                                              FlashBulbConfiguration config )
        {
            if( ((IFlashBulbConfiguration)config).ComputedValid is null )
            {
                Throw.CKException( "CheckValid has been called." );
            }

            bool colorChanged = config.FlashColor != CurrentConfiguration.FlashColor;
            bool configHasChanged = colorChanged || config.FlashRate != CurrentConfiguration.FlashRate;

            if( colorChanged && _colorFromConfig )
            {
                _color = config.FlashColor;
            }

            return Task.FromResult( configHasChanged
                                        ? DeviceReconfiguredResult.UpdateSucceeded
                                        : DeviceReconfiguredResult.None );
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

        protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            switch( command )
            {
                case FlashCommand f:
                    // ...Do whatever is needed here to make the FlashBulb flash using
                    // the current _color and CurrentConfiguration.FlashRate...
                    await _testFlash.SafeRaiseAsync( monitor, this, _color );
                    f.Completion.SetResult();
                    return;
                case SetFlashColorCommand c:
                    {
                        var prevColor = _color;
                        if( c.Color != null )
                        {
                            _color = c.Color.Value;
                            _colorFromConfig = false;
                        }
                        else
                        {
                            _color = CurrentConfiguration.FlashColor;
                            _colorFromConfig = true;
                        }
                        c.Completion.SetResult( prevColor );
                        return;
                    }
            }
            // The base.DoHandleCommandAsync throws a NotSupportedException: all defined commands MUST be handled above.
            await base.DoHandleCommandAsync( monitor, command );
        }

        protected override Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            Interlocked.Increment( ref OnCommandComplededCount );
            return base.OnCommandCompletedAsync( monitor, command );
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Interlocked.Decrement( ref TotalCount );
            _testFlash.RemoveAll();
            return Task.CompletedTask;
        }
    }

}
