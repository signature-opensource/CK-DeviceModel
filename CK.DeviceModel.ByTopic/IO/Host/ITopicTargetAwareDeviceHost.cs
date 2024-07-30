using CK.Core;
using CK.DeviceModel.ByTopic.IO.Commands;
using System.Threading.Tasks;

namespace CK.DeviceModel.ByTopic.IO.Host
{
    [IsMultiple]
    public interface ITopicTargetAwareDeviceHost
    {

        /// <summary>
        /// A device full name is "DeviceHostTypeName/DeviceName".
        /// This can be all possible devices. Example: "LEDStripHost" (all devices of type LEDStrip) or "LEDStripHost/Wall0"
        /// </summary>
        string DeviceFullName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="cmd"></param>
        /// <returns>
        /// Returns true if the command has been handled, false if the command is not handled by this device.
        /// This throws on error.
        /// </returns>
        ValueTask<bool> HandleAsync( IActivityMonitor monitor, ICommandPartDeviceTopicTarget cmd );
    }
}
