using CK.Cris;
using CK.DeviceModel.ByTopic.IO.Commands;
using System;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ITurnOnLocationCommand : ICommand, ICommandPartDeviceTopicTarget
    {
        ColorLocation Color { get; set; }
        bool IsBlinking { get; set; }
        TimeSpan TurnOfAfter { get; set; }
    }
}
