using CK.Core;
using CK.IO.DeviceModel;
using System.Text.RegularExpressions;

namespace CK.Cris.DeviceModel;

public sealed partial class Validators : IAutoService
{
    [IncomingValidator]
    public void Normalize( UserMessageCollector collector, ICommandDeviceTopics cmd )
    {
        var topics = cmd.Topics.ToList();
        for( int i = 0; i < topics.Count; i++ )
        {
            var topic = topics[i];
            if( !string.IsNullOrEmpty( topic ) && topic[0] == '/' )
            {
                collector.Warn( $"Topic {topic} should not start with a '/'." );
                cmd.Topics[i] = cmd.Topics[i].Substring( 1 );
            }
        }
    }
}
