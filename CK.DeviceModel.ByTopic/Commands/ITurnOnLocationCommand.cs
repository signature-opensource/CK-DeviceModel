using CK.Cris;
using CK.DeviceModel.ByTopic.IO.Commands;
using System;
using System.Collections.Generic;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ITurnOnLocationCommand : ICommand<ISwitchLocationCommandResult>, ICommandDeviceTopicTarget
    {
        List<ITopicColor> Colors { get; set; }
        TimeSpan TurnOfAfter { get; set; }
    }
}
