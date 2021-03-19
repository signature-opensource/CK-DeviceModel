using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests.Std
{
    public class StdSampleConfiguration : StdDeviceConfiguration
    {
        public StdSampleConfiguration()
        {
        }

        protected StdSampleConfiguration( DeviceConfiguration source )
            : base( source )
        {
        }
    }

    public class StdSampleHost : StdDeviceHost<StdSample, DeviceHostConfiguration<StdSampleConfiguration>, StdSampleConfiguration>
    {
        public StdSampleHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }
    }

    public class StupidCommand : HostedDeviceCommand<StdSampleHost>
    {
        public string? Message { get; set; }
    }

    public class StupidCommandWithResult : HostedDeviceCommand<StdSampleHost,string>
    {
        public int Power { get; set; }
    }

    public class StdSample : StdDevice<StdSampleConfiguration>
    {
        public StdSample( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            return Task.CompletedTask;
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, StdSampleConfiguration config )
        {
            return Task.FromResult( DeviceReconfiguredResult.UpdateSucceeded );
        }

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            return Task.FromResult( false );
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            return Task.CompletedTask;
        }

        protected override Task HandleCommandAsync( IActivityMonitor monitor, DeviceCommand command, CancellationToken token )
        {
            if( command is StupidCommand s )
            {
                monitor.Info( s.Message );
                return Task.CompletedTask;
            }
            return base.HandleCommandAsync( monitor, command, token );
        }


        Task<int> ExecuteAsync( StupidCommandWithResult c )
        {
            return Task.FromResult( c.Power * 2 );
        }
    }
}
