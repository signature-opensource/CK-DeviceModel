using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Resets the SimpleScale.
    /// This command can be executed while the device is stopped and always
    /// raises a <see cref="SimpleScaleResetEvent"/>.
    /// </summary>
    public class SimpleScaleResetCommand : DeviceCommand<SimpleScaleHost>
    {

        protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
    }
}
