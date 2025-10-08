using CK.Cris;

namespace CK.IO.DeviceModel;

public interface ICommandDeviceName : ICommandPart
{
    public string DeviceName { get; set; }
}
