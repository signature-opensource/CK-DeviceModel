using CK.Core;
using CK.Cris;

namespace CK.IO.DeviceModel;

public class IncomingValidators : IAutoService
{
    [IncomingValidator]
    public virtual void ValidateDeviceNameCommand( ICommandDeviceName cmd, UserMessageCollector collector )
    {
        if( string.IsNullOrWhiteSpace( cmd.DeviceName ) )
        {
            collector.Error( "Invalid device name.", "Device.InvalidDeviceName" );
        }
    }
}
