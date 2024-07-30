using CK.Core;
using CK.Cris;
using System.Collections.Generic;

namespace CK.DeviceModel.ByTopic.Commands
{
    public interface ISwitchLocationCommandResult : IPoco
    {
        public string Topic { get; set; }
        public Dictionary<string,bool> ResultByDeviceName { get; set; }
    }
}
