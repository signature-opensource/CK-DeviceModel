using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;


namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class SendingTimeUtcTests
    {
        [Test]
        public void SendingTimeUtc_and_ImmediateSending_are_exclusive()
        {
            var d = DateTime.UtcNow;

            var cmd = new FlashCommand();
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.SendingTimeUtc = d;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().Be( d );

            cmd.ImmediateSending = true;
            cmd.ImmediateSending.Should().BeTrue();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.SendingTimeUtc = d;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().Be( d );

            cmd.SendingTimeUtc = null;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.ImmediateSending = true;
            cmd.SendingTimeUtc = CK.Core.Util.UtcMinValue;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            FluentActions.Invoking( () => cmd.SendingTimeUtc = DateTime.Now ).Should().Throw<ArgumentException>();
        }


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
                DeltaMS = r.ReadInt32();
                ExecTimeMS = r.ReadInt32();
            }

            public int DeltaMS { get; set; }

            public int ExecTimeMS { get; set; }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.Write( DeltaMS );
                w.Write( ExecTimeMS );
            }
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
                Traces = new List<string>();
            }

            public List<string> Traces { get; }
            public int ReminderCount;

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                Traces.Add( $"Destroy" );
                return Task.CompletedTask;
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
            {
                Traces.Add( $"Reconfigure" );
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

            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    Traces.Add( $"Command {cmd.Trace}" );
                    await Task.Delay( CurrentConfiguration.ExecTimeMS, cmd.CancellationToken ).ConfigureAwait( false );
                    cmd.Completion.SetResult();
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command );
            }

            protected override Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    Traces.Add( $"Completed: Command {cmd.Trace}" );
                    AddReminder( DateTime.UtcNow.AddMilliseconds( CurrentConfiguration.DeltaMS ), cmd.Trace );
                    ++ReminderCount;
                }
                return Task.CompletedTask;
            }

            protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state )
            {
                Traces.Add( $"Reminder: Command {state}, Delta:{(DateTime.UtcNow - reminderTimeUtc).TotalMilliseconds} ms." );
                return Task.CompletedTask;
            }
        }

        public class DCommand : DeviceCommand<DHost>
        {
            public string? Trace { get; set; }

            public override string ToString() => $"{base.ToString()} - {Trace}";
        }

        [TestCase(50, 200, 20)]
        [TestCase(50, 150, 20)]
        public async Task SendingTimeUtc_stress_test_Async( int nb, int deltaMS, int execTimeMS )
        {
            var rnd = new Random();
            var dSpan = TimeSpan.FromMilliseconds( deltaMS );

            void SendCommands( D device, bool? inc, List<DCommand> c )
            {
                var mode = inc switch { true => "increased", false => "decreased", _ => "random" };
                using var g = TestHelper.Monitor.OpenInfo( $"Sending {nb} command with {mode} start time." );
                var start = DateTime.UtcNow.Add( dSpan );
                var commands = Enumerable.Range( 0, nb )
                               .Select( i => new DCommand() { Trace = $"{mode}-n°{i}", SendingTimeUtc = start.Add( dSpan * i ) } )
                               .ToArray();
                if( !inc.HasValue )
                {
                    int i = nb;
                    while( i > 1 )
                    {
                        int k = rnd.Next( i-- );
                        var temp = commands[i];
                        commands[i] = commands[k];
                        commands[k] = temp;
                    }
                }
                else if( !inc.Value )
                {
                    Array.Reverse( commands );
                }
                c.AddRangeArray( commands );
                foreach( var cmd in commands ) device.UnsafeSendCommand( TestHelper.Monitor, cmd );
            }

            var h = new DHost();
            var config = new DConfiguration()
            {
                Name = "Single",
                Status = DeviceConfigurationStatus.RunnableStarted,
                ExecTimeMS = execTimeMS,
                DeltaMS = deltaMS
            };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["Single"];
            Debug.Assert( d != null );

            var all = new List<DCommand>();
            SendCommands( d, false, all );
            SendCommands( d, true, all );
            SendCommands( d, null, all );

            // We have 2 execTimeMS per command: one for the delay in DoHandleCommandAsync and one for the reminder.
            // The first one "blocks" the device, the other one is "in parallel" since it is a reminder (all of them can fire "at the same time").
            int minimalExecTime = (nb * 3) * execTimeMS;
            // The latest starts at nb*deltaMS and spans 2 execTimeMS.
            int latestMinimalTime = nb * deltaMS + 2 * execTimeMS;
            int minTime = Math.Max( minimalExecTime, latestMinimalTime );
            TestHelper.Monitor.Info( $"Waiting for {minTime*3} ms." );
            // This is very theoretical. But we should not wait 10% this time for the last reminder to be executed.
            // This depends on the machine and on the deltaMS: for small deltaMS this doesn't work!
            await Task.Delay( (minTime * 110)/100 );
            TestHelper.Monitor.Info( $"Done waiting." );

            d.ReminderCount.Should().Be( nb * 3 );
        }
    }
}
