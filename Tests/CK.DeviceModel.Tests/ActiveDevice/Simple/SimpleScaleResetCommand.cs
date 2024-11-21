using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

/// <summary>
/// Resets the <see cref="SimpleScale"/>.
/// This command can be executed while the device is stopped and always
/// raises a <see cref="SimpleScaleResetEvent"/>.
/// <para>
/// Since this event has no data, it is useless to consider that it is the result of this command.
/// </para>
/// </summary>
public class SimpleScaleResetCommand : DeviceCommand<SimpleScaleHost>
{
    protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
}
