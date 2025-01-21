using CK.Cris;

namespace CK.IO.DeviceModel;

/// <summary>
/// Command part that targets anything that is bound to a topic mangaed by any kind of device host,
/// a specific device host or a specific device.
/// </summary>
public interface ICommandDeviceTopics : ICommandPart, ICommand<IStandardResultPart>
{
    List<string> Topics { get; }
}
