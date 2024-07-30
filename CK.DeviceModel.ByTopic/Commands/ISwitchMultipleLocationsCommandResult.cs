using CK.Core;
using System.Collections.Generic;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ISwitchMultipleLocationsCommandResult : IPoco
    {
        public IList<ISwitchLocationCommandResult> Results { get; }
    }
}
