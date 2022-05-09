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
    public class SendingTimeUtcTests
    {
        [Test]
        [Timeout( 200 )]
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
                ExecTimeMS = r.ReadInt32();
            }

            public int ExecTimeMS { get; set; }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
                w.Write( (byte)0 );
                w.Write( ExecTimeMS );
            }
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
            }

            public int ReminderCount;
            public int ReminderFiredCount;

            protected override Task DoDestroyAsync( IActivityMonitor monitor )
            {
                return Task.CompletedTask;
            }

            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config )
            {
                return Task.FromResult( DeviceReconfiguredResult.None );
            }

            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
            {
                return Task.FromResult( true );
            }

            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
            {
                return Task.CompletedTask;
            }

            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    monitor.Trace( $"Handling command '{cmd}': Waiting {CurrentConfiguration.ExecTimeMS} ms before completing it." );
                    await Task.Delay( CurrentConfiguration.ExecTimeMS, cmd.CancellationToken ).ConfigureAwait( false );
                    monitor.Trace( $"Completing command '{cmd}'. (ReminderCount so far {ReminderCount})" );
                    cmd.Completion.SetResult();
                    return;
                }
                if( command is GetReminderCountCommand get )
                {
                    get.Completion.SetResult( (ReminderCount,ReminderFiredCount) );
                    monitor.Trace( $"ReminderCount is '({ReminderCount},{ReminderFiredCount})'." );
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command );
            }

            protected override Task OnCommandCompletedAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is DCommand cmd )
                {
                    AddReminder( DateTime.UtcNow.AddMilliseconds( 50 ), cmd.Trace );
                    ++ReminderCount;
                    monitor.Trace( $"Completed: Command '{cmd}'. Added Reminder to fire in 50 ms (ReminderCount = {ReminderCount})." );
                }
                return Task.CompletedTask;
            }

            protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state )
            {
                ++ReminderFiredCount;
                monitor.Trace( $"Reminder: Command {state}, Delta:{(int)(DateTime.UtcNow - reminderTimeUtc).TotalMilliseconds} ms. (ReminderCount is {ReminderCount}, ReminderFiredCount is {ReminderFiredCount}.)" );
                return Task.CompletedTask;
            }
        }

        public class DCommand : DeviceCommand<DHost>
        {
            public string? Trace { get; set; }

            protected override string? ToStringSuffix => Trace;
        }

        public class GetReminderCountCommand : DeviceCommand<DHost,(int,int)>
        {
        }

        [TestCase(50, 200, 20)]
        [TestCase(50, 150, 20)]
        [Timeout( 16000 )]
        public async Task SendingTimeUtc_stress_test_Async( int nb, int sendingDeltaMS, int execTimeMS )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( SendingTimeUtc_stress_test_Async )}({nb},{sendingDeltaMS},{execTimeMS})" );

            var rnd = new Random();
            var dSpan = TimeSpan.FromMilliseconds( sendingDeltaMS );

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
                ExecTimeMS = execTimeMS
            };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["Single"];
            Debug.Assert( d != null );

            // The last command (n° nb) will start in nb * sendingDeltaMS and wait for execTimeMS before adding the reminder.
            int lastAddedReminderTime = (nb * sendingDeltaMS) + execTimeMS;
            int lastReminderTime = lastAddedReminderTime + 50;
            // This is very theoretical. But we should not wait 20% this time more for the last reminder to be executed.
            // This depends on the machine and on the deltaMS: for small deltaMS this doesn't work!
            int waitTime = (lastReminderTime * 120) / 100;

            var all = new List<DCommand>();
            SendCommands( d, false, all );
            SendCommands( d, true, all );
            SendCommands( d, null, all );

            // Adds 200 ms since this test regularly fails on the CI runners.
            TestHelper.Monitor.Info( $"All the commands have been sent. Waiting for {waitTime} + 200 ms." );
            await Task.Delay( waitTime + 200 );
            TestHelper.Monitor.Info( $"Done waiting." );

            int rc = d.ReminderCount;
            if( rc == 0 )
            {
                TestHelper.Monitor.Warn( "Read 0 ReminderCount! Using Volatile.Read." );
                rc = Volatile.Read( ref d.ReminderCount );
                if( rc == 0 )
                {
                    TestHelper.Monitor.Warn( "Using Volatile.Read: still 0. Using a GetReminderCountCommand." );
                    var c = new GetReminderCountCommand();
                    d.UnsafeSendCommand( TestHelper.Monitor, c );
                    rc = (await c.Completion).Item1;
                }
            }
            int fc = d.ReminderFiredCount;
            if( fc == 0 )
            {
                TestHelper.Monitor.Warn( "Read 0 ReminderFiredCount! Using Volatile.Read." );
                fc = Volatile.Read( ref d.ReminderFiredCount );
                if( fc == 0 )
                {
                    TestHelper.Monitor.Warn( "Using Volatile.Read: still 0. Using a GetReminderCountCommand." );
                    var c = new GetReminderCountCommand();
                    d.UnsafeSendCommand( TestHelper.Monitor, c );
                    fc = (await c.Completion).Item2;
                }
            }
            TestHelper.Monitor.Info( $"Final ReminderCount = {rc}, ReminderFiredCount = {fc}." );
            rc.Should().Be( nb * 3, "ReminderCount is fine." );
            fc.Should().Be( nb * 3, "ReminderFiredCount is fine." );
        }
    }
}
