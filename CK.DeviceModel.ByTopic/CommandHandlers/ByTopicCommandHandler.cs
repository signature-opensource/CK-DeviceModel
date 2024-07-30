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
            return _hosts.Where( x => x.DeviceFullName.StartsWith( deviceFullName ) );
        }

        [CommandHandler]
        public async Task<ISwitchLocationCommandResult> HandleTurnOnLocationCommandAsync( IActivityMonitor monitor, ITurnOnLocationCommand cmd )
        {
            var targets = ForDeviceFullName( cmd.DeviceFullName );
            var resultByDeviceName = new Dictionary<string, bool>();
            foreach( var host in targets )
            {
                var result = await host.HandleAsync( monitor, cmd ).ConfigureAwait( false );
                resultByDeviceName.TryAdd( host.DeviceFullName, result );
            }

            return cmd.CreateResult( r =>
             {
                 r.Topic = cmd.Topic;
                 r.ResultByDeviceName = resultByDeviceName;
             } );
        }

        [CommandHandler]
        public async Task<ISwitchMultipleLocationsCommandResult> HandleTurnOnMultipleLocationsCommandAsync( IActivityMonitor monitor, ITurnOnMultipleLocationsCommand cmd )
        {
            var turnOnLocationCommandResults = new List<ISwitchLocationCommandResult>();
            foreach( var c in cmd.Locations )
            {
                turnOnLocationCommandResults.Add( await HandleTurnOnLocationCommandAsync( monitor, c ));
            }

            return cmd.CreateResult( r =>
            {
                r.Results.AddRange(turnOnLocationCommandResults);
            } );
        }


        [CommandHandler]
        public async Task<ISwitchLocationCommandResult> HandleTurnOffLocationCommandAsync( IActivityMonitor monitor, ITurnOffLocationCommand cmd )
        {
            var targets = ForDeviceFullName( cmd.DeviceFullName );
            var resultByDeviceName = new Dictionary<string, bool>();
            foreach( var host in targets )
            {
                var result = await host.HandleAsync( monitor, cmd ).ConfigureAwait( false );
                resultByDeviceName.TryAdd( host.DeviceFullName, result );
            }

            return cmd.CreateResult( r =>
            {
                r.Topic = cmd.Topic;
                r.ResultByDeviceName = resultByDeviceName;
            } );
        }

        [CommandHandler]
        public async Task<ISwitchMultipleLocationsCommandResult> HandleTurnOffMultipleLocationsCommandAsync( IActivityMonitor monitor, ITurnOffMultipleLocationsCommand cmd )
        {
            var turnOffLocationCommandResults = new List<ISwitchLocationCommandResult>();
            foreach( var c in cmd.Locations )
            {
                turnOffLocationCommandResults.Add( await HandleTurnOffLocationCommandAsync( monitor, c ) );
            }

            return cmd.CreateResult( r =>
            {
                r.Results.AddRange(turnOffLocationCommandResults);
            } );
        }

    }
}
