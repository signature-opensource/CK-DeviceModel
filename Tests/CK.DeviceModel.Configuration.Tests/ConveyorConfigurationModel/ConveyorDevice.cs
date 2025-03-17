#pragma warning disable CA2211 // Non-constant fields should not be visible

using CK.Core;
using System.Threading.Tasks;
using System;

namespace CK.DeviceModel.Configuration.Tests;


public class ConveyorDevice : Device<ConveyorDeviceConfiguration>
{
    public ConveyorDevice( IActivityMonitor monitor, CreateInfo info )
        : base( monitor, info )
    {
    }

    protected override Task DoDestroyAsync( IActivityMonitor monitor ) => throw new NotImplementedException();
    protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, ConveyorDeviceConfiguration config ) => throw new NotImplementedException();
    protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason ) => throw new NotImplementedException();
    protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason ) => throw new NotImplementedException();
}
