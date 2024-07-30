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

        public List<string> Topics { get; set; }

        public FakeSignatureDeviceHosts()
        {
            DeviceFullName = nameof( FakeSignatureDeviceHosts );
            Topics = new List<string>()
            {
                "Test",
                "Test-1",
                "Test-FakeSignatureDevice",
            };
        }

        public async ValueTask<bool> HandleAsync( IActivityMonitor monitor, ICommandPartDeviceTopicTarget cmd )
        {
            if( !Topics.Contains( cmd.Topic ) )
            {
                return false;
            }

            if( cmd is ITurnOffLocationCommand )
            {
                return true;
            }
            else if( cmd is ITurnOnLocationCommand )
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
