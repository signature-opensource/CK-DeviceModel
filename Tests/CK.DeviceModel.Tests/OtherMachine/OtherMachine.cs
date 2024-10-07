using CK.Core;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace CK.DeviceModel.Tests;

public class OtherMachine : Device<OtherMachineConfiguration>
{
    public static int TotalCount;
    public static int TotalRunning;

    public OtherMachine( IActivityMonitor monitor, CreateInfo info )
        : base( monitor, info )
    {
        Interlocked.Increment( ref TotalCount );
    }

    protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, OtherMachineConfiguration config )
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
