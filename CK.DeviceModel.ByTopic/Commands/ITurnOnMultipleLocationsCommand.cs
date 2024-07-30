using CK.Cris;
using System.Collections.Generic;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ITurnOnMultipleLocationsCommand : ICommand<ISwitchMultipleLocationsCommandResult>
    {
        IList<ITurnOnLocationCommand> Locations { get; }
    }
}
