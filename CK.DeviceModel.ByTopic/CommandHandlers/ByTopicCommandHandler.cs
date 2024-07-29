using CK.Core;
using CK.Cris;
using CK.DeviceModel.ByTopic.Commands;
using CK.DeviceModel.ByTopic.IO.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel.ByTopic.CommandHandlers
{
    public class ByTopicCommandHandler : IAutoService
    {
        readonly IEnumerable<ITopicTargetAwareDeviceHost> _hosts;

        public ByTopicCommandHandler( IEnumerable<ITopicTargetAwareDeviceHost> hosts )
        {
            _hosts = hosts;
        }

        IEnumerable<ITopicTargetAwareDeviceHost> ForDeviceFullName( string? deviceFullName )
        {
            if( string.IsNullOrWhiteSpace( deviceFullName ) ) return _hosts;
            return _hosts.Where(x=> x.DeviceFullName.StartsWith(deviceFullName));
        }

        [CommandHandler]
        public async Task HandleTurnOnLocationCommandAsync( IActivityMonitor monitor, ITurnOnLocationCommand cmd )
        {
            var targets = ForDeviceFullName( cmd.DeviceFullName );
            foreach( var host in targets )
            {
                await host.HandleAsync( monitor, cmd ).ConfigureAwait( false );
            }
        }

        [CommandHandler]
        public async Task HandleTurnOnMultipleLocationsCommandAsync( IActivityMonitor monitor, ITurnOnMultipleLocationsCommand commands )
        {
            foreach( var c in commands.Locations )
            {
                await HandleTurnOnLocationCommandAsync( monitor, c );
            }
        }


        [CommandHandler]
        public async Task HandleTurnOffLocationCommandAsync( IActivityMonitor monitor, ITurnOffLocationCommand cmd )
        {
            var targets = ForDeviceFullName( cmd.DeviceFullName );
            foreach( var host in targets )
            {
                await host.HandleAsync( monitor, cmd ).ConfigureAwait( false );
            }
        }

        [CommandHandler]
        public async Task HandleTurnOffMultipleLocationsCommandAsync( IActivityMonitor monitor, ITurnOffMultipleLocationsCommand commands )
        {
            foreach( var c in commands.Locations )
            {
                await HandleTurnOffLocationCommandAsync( monitor, c );
            }
        }

    }
}
