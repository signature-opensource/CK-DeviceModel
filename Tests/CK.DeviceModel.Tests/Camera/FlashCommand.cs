using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// FLash is an asynchronous command.
    /// Any command that must triiger an event must be asynchronous since
    /// perfect events must be aysnchronous.
    /// </summary>
    public class FlashCommand : AsyncDeviceCommand<CameraHost>
    {
    }
}
