using CK.Core;
using CK.DeviceModel.ByTopic.Commands;
using CK.DeviceModel.ByTopic.IO.Commands;
using CK.DeviceModel.ByTopic.IO.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel.ByTopic.Tests.Hosts
{
    public class FakeSignatureDeviceHosts : IAutoService, ITopicTargetAwareDeviceHost
    {
        public string DeviceFullName { get; set; }

        public FakeSignatureDeviceHosts()
        {
            DeviceFullName = $"FakeSignatureDeviceHosts/{Guid.NewGuid()}";
        }

        public async ValueTask<bool> HandleAsync( IActivityMonitor monitor, ICommandPartDeviceTopicTarget cmd )
        {
            if( cmd is ITurnOffLocationCommand )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
