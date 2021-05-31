using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// The command returns the previous color.
    /// </summary>
    public class SetFlashColorCommand : DeviceCommand<CameraHost,int>
    {
        /// <summary>
        /// The new color to set.
        /// </summary>
        public int Color { get; set; }
    }
}
