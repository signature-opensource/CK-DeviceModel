using CK.Cris;

namespace CK.DeviceModel.ByTopic.IO.Commands
{
    public interface ICommandPartDeviceTopicTarget : ICommandPart
    {
        /// <summary>
        /// Gets or sets a the topic.
        /// An empty topic applies to all locations.
        /// <para>
        /// A topic is a '/' separated strings. A leading '/' is ignored.
        /// </para>
        /// </summary>
        string Topic { get; set; }

        /// <summary>
        /// Optional target host and/or device.
        /// A device full name is "DeviceHostTypeName/DeviceName".
        /// This can be null (all possible devices), "LEDStripHost" (all devices of type LEDStrip) or "LEDStripHost/Wall0"
        /// (only this device).
        /// </summary>
        string? DeviceFullName { get; set; }
    }
}
