using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Here, recordinf command is synchronous.
    /// Being synchronous is possible as long as no event must be raised by the handling of the command.
    /// </summary>
    public class SetFlashColorCommand : SyncDeviceCommand<CameraHost>
    {
        public int Color { get; set; }
    }
}
