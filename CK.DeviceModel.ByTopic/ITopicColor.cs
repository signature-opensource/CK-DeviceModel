using CK.Core;

namespace CK.DeviceModel.ByTopic
{
    public interface ITopicColor : IPoco
    {
        public ColorLocation Color { get; set; }
        public bool IsBlinking { get; set; }
    }
}
