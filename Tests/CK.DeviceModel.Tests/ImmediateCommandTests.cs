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

            public DConfiguration( ICKBinaryReader r )
                : base( r )
            {
                r.ReadByte();
                Trace = r.ReadNullableString();
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.WriteNullableString( Trace );
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


        [TestCase( 40, 7784 )]
        public async Task BaseImmediateCommandLimit_and_ImmediateCommandLimitOffset_works( int nb, int seed )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( sending_immediate_commands_does_not_block_the_loop )}-{nb}-{seed}" );

            static List<DCommand> SendCommands( IDevice d, int nb )
            {
                var commands = new List<DCommand>();
                for( int i = 0; i < nb; ++i )
                {
                    var cI = new DCommand()
                    {
                        DeviceName = "D",
                        Trace = $"I°{i}",
                        ExecutionTime = i == 0 ? 50 : 0
                    };
                    commands.Add( cI );
                    d.SendCommandImmediate( TestHelper.Monitor, cI ).Should().BeTrue();
                    var cRegular1 = new DCommand()
                    {
                        DeviceName = "D",
                        Trace = $"N°{i}",
                    };
                    commands.Add( cRegular1 );
                    d.SendCommand( TestHelper.Monitor, cRegular1 ).Should().BeTrue();
                    var cRegular2 = new DCommand()
                    {
                        DeviceName = "D",
                        Trace = $"N°{i}-2",
                    };
                    commands.Add( cRegular2 );
                    d.SendCommand( TestHelper.Monitor, cRegular2 ).Should().BeTrue();
                }
                return commands;
            }

            static void CheckCommandTraces( List<string> traces, int limit, int nb )
            {
                var profile = traces.Where( t => t.StartsWith( "Command " ) ).Select( t => t[8] ).ToArray();
                profile.Length.Should().Be( 3 * nb, "We have nb 'I' and 2*nb 'N'." );
                profile.All( c => c == 'I' || c == 'N' ).Should().BeTrue();
                profile.Count( c => c == 'I' ).Should().Be( nb );
                // 'I' should occur limit times, with one N between them and the remaining must be N only.
                var oneBlock = Enumerable.Repeat( 'I', limit ).Append( 'N' ).ToArray();
                var inspector = profile.AsSpan();
                do
                {
                    int blockLen = limit + 1;
                    bool matchBlock = inspector.Slice( 0, blockLen ).SequenceEqual( oneBlock );
                    if( !matchBlock )
                    {
                        blockLen = (nb % limit) + 1;
                        blockLen.Should().BeGreaterThan( 1, "The last block must not match because of nb is not a factor of limit." );
                        matchBlock = inspector.Slice( 0, blockLen ).SequenceEqual( Enumerable.Repeat( 'I', blockLen - 1 ).Append( 'N' ).ToArray() );
                    }
                    matchBlock.Should().BeTrue();
                    inspector = inspector.Slice( blockLen );
                }
                while( inspector[0] == 'I' );
                inspector.ToArray().All( c => c == 'N' );
            }

            var h = new DHost();
            var config = new DConfiguration()
            {
                Name = "D",
                Status = DeviceConfigurationStatus.RunnableStarted,
                BaseImmediateCommandLimit = 5
            };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["D"];
            Debug.Assert( d != null && d.IsRunning );

            foreach( var c in SendCommands( d, nb ) ) await c.Completion;

            CheckCommandTraces( d.Traces, 5, nb );

            d.Traces.Clear();
            // This results in a 1 actual limit.
            d.ImmediateCommandLimitOffset = -13;
            foreach( var c in SendCommands( d, nb ) ) await c.Completion;
            CheckCommandTraces( d.Traces, 1, nb );

            d.Traces.Clear();
            // Limit is 7 since configuration corrects it.
            config.BaseImmediateCommandLimit = 13 + 7;
            (await d.ReconfigureAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            d.Traces.Should().BeEquivalentTo( "Reconfigure " );
            d.Traces.Clear();

            foreach( var c in SendCommands( d, nb ) ) await c.Completion;
            CheckCommandTraces( d.Traces, 7, nb );
        }

    }
}
