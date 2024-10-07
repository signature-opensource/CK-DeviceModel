using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests;

/// <summary>
/// This command sets the color of the flash (or resets it to the configured
/// color) and returns the previous color.
/// </summary>
public class SetFlashColorCommand : DeviceCommand<FlashBulbHost, int>
{
    /// <summary>
    /// The new color to set.
    /// Null to reset the color to the <see cref="FlashBulbConfiguration.FlashColor"/>.
    /// </summary>
    public int? Color { get; set; }
}
