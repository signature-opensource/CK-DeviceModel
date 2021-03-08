using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    public class SetFlashColorCommand : DeviceCommand<CameraHost>
    {
        public int Color { get; set; }
    }
}
