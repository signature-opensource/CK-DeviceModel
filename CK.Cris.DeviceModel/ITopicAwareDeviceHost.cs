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
    /// Handle the ICommandDeviceTopics for each device host that implements the interface.
    /// </summary>
    /// <param name="monitor"></param>
    /// <param name="userMessageCollector"></param>
    /// <param name="cmd"></param>
    ValueTask HandleAsync( IActivityMonitor monitor, UserMessageCollector userMessageCollector, ICommandDeviceTopics cmd );
}
