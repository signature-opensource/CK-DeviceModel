using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public abstract partial class Device<TConfiguration>
    {
        //readonly Channel<SyncDeviceCommand> _commands;

        //protected void SendCommand( SyncDeviceCommand cmd )
        //{
        //    _commands.Writer.TryWrite( cmd );
        //}

        //async Task RunLoop()
        //{

        //}
    }
}
