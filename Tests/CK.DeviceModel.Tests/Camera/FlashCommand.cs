using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// This command triggers a flash on the camera (and raise our <see cref="Camera.Flash"/> event).
    /// </summary>
    public class FlashCommand : DeviceCommand<CameraHost>
    {
    }
}
