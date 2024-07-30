using CK.Core;
using CK.Cris;
using CK.DeviceModel.ByTopic.IO.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CK.DeviceModel.ByTopic.IO
{
    public sealed partial class Validators : IAutoService
    {
        static Regex _deviceFullName = new Regex( @"\w+(/\w+)?", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant );


        [IncomingValidator]
        public void Normalize( UserMessageCollector collector, ICommandPartDeviceTopicTarget cmd )
        {
            if( cmd.DeviceFullName != null && !_deviceFullName.IsMatch( cmd.DeviceFullName ) )
            {
                collector.Error( $"Invalid DeviceFullName." );
            }
            if( !string.IsNullOrEmpty( cmd.Topic ) && cmd.Topic[0] == '/' )
            {
                collector.Warn( "Topic should not start with a '/'." );
                cmd.Topic = cmd.Topic.Substring( 1 );
            }
        }

    }
}
