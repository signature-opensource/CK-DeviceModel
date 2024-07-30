using CK.Cris;
using CK.DeviceModel.ByTopic.IO.Commands;
using System;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ITurnOffLocationCommand : ICommand<ISwitchLocationCommandResult>, ICommandPartDeviceTopicTarget
    {
    }
}
