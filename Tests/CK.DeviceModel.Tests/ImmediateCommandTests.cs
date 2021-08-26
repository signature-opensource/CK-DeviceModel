using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class ImmediateCommandTests
    {
        public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
        {
        }

        public class DConfiguration : DeviceConfiguration
        {
            public DConfiguration()
            {
            }

            public DConfiguration( DConfiguration other )
                : base( other )
            {
                Trace = other.Trace;
            }

            public string? Trace { get; set; }
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Traces = new List<string>();
            }

            public List<string> Traces { get; }

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                Traces.Add( $"Destroy" );
                return Task.CompletedTask;
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
            {
                Traces.Add( $"Reconfigure {config.Trace}" );
                return Task.FromResult( DeviceReconfiguredResult.None );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
            {
                Traces.Add( $"Start {reason}" );
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
            {
                Traces.Add( $"Stop {reason}" );
                return Task.CompletedTask;
            }

            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command, CancellationToken token )
            {
                if( command is DCommand cmd )
                {
                    Traces.Add( $"Command {cmd.Trace}" );
                    await Task.Delay( cmd.ExecutionTime );
                    cmd.Completion.SetResult();
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command, token );
            }
        }


        public class DCommand : DeviceCommand<DHost>
        {
            public string? Trace { get; set; }

            public int ExecutionTime { get; set; }

            protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.AlwaysWaitForNextStart;

            public override string ToString() => $"{base.ToString()} - {Trace}";
        }

        [TestCase( 40, 7784 )]
        public async Task sending_immediate_commands_does_not_block_the_loop( int nb, int seed )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( sending_immediate_commands_does_not_block_the_loop )}-{nb}-{seed}" );

            var h = new DHost();
            var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.RunnableStarted };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["First"];
            Debug.Assert( d != null && d.IsRunning );
            Random random = new Random( seed );

            var commands = new List<DCommand>();
            int immediateNumber = 0;
            int normalNumber = 0;
            for( int i = 0; i < nb; ++i )
            {
                int nbNormal = random.Next( 9 );
                while( --nbNormal >= 0 )
                {
                    var c = new DCommand()
                    {
                        DeviceName = "First",
                        Trace = $"n°{++normalNumber}",
                        ExecutionTime = random.Next( 30 )
                    };
                    commands.Add( c );
                    d.SendCommand( TestHelper.Monitor, c ).Should().BeTrue();
                }
                int nbImmediate = random.Next( 2 );
                while( --nbImmediate >= 0 )
                {
                    var c = new DCommand()
                    {
                        DeviceName = "First",
                        Trace = $"Immediate n°{++immediateNumber}",
                        ExecutionTime = random.Next( 30 )
                    };
                    commands.Add( c );
                    d.SendCommandImmediate( TestHelper.Monitor, c ).Should().BeTrue();
                }
                await Task.Delay( random.Next( 200 ) );
            }

            foreach( var c in commands )
            {
                await c.Completion;
                c.Completion.IsCompleted.Should().BeTrue( c.Trace );
                c.Completion.Task.IsCompletedSuccessfully.Should().BeTrue( c.Trace );
            }

        }

    }
}
