using CK.Cris;

namespace CK.DeviceModel.ByTopic.IO.Commands
{
    /// <summary>
    /// Command part that targets anything that is bound to a topic mangaed by any kind of device host,
    /// a specific device host or a specific device.
    /// </summary>
    public interface ICommandDeviceTopicTarget : ICommandPart
    {
        /// <summary>
        /// Gets or sets the topic.
        /// An empty topic targets all items.
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
