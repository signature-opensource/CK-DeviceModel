using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Resets the Scale.
    /// This command can be executed while the device is stopped and always
    /// raises a <see cref="ScaleResetEvent"/>.
    /// <para>
    /// Since this event has no data, it is useless to consider that it is the result of this command.
    /// </para>
    /// </summary>
    public class ScaleResetCommand : DeviceCommand<ScaleHost>
    {
        protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
    }
}
