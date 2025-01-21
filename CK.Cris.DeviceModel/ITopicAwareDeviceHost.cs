using CK.Core;
using CK.IO.DeviceModel;

namespace CK.Cris.DeviceModel;

[IsMultiple]
public interface ITopicAwareDeviceHost
{

    /// <summary>
    /// A device host name is "DeviceHostTypeName".
    /// This can be all possible devices host. Example: "LEDStripHost" or "SignatureDeviceHost"
    /// </summary>
    string DeviceHostName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="monitor"></param>
    /// <param name="cmd"></param>
    /// <returns>
    /// Returns true if the command has been handled, false if the command is not handled by this device.
    /// This throws on error.
    /// </returns>
    ValueTask<bool> HandleAsync( IActivityMonitor monitor, ICommandDeviceTopics cmd );
}
