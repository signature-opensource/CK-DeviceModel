using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;


namespace CK.DeviceModel.Tests;

[TestFixture]
public class SendingTimeUtcTests
{
    [Test]
    [CancelAfter( 200 )]
    public void SendingTimeUtc_and_ImmediateSending_are_exclusive( CancellationToken cancellation )
    {
        var d = DateTime.UtcNow;

        var cmd = new FlashCommand();
        cmd.ImmediateSending.ShouldBeFalse();
        cmd.SendingTimeUtc.ShouldBeNull();

        cmd.SendingTimeUtc = d;
        cmd.ImmediateSending.ShouldBeFalse();
        cmd.SendingTimeUtc.ShouldBe( d );

        cmd.ImmediateSending = true;
        cmd.ImmediateSending.ShouldBeTrue();
        cmd.SendingTimeUtc.ShouldBeNull();

        cmd.SendingTimeUtc = d;
        cmd.ImmediateSending.ShouldBeFalse();
        cmd.SendingTimeUtc.ShouldBe( d );

        cmd.SendingTimeUtc = null;
        cmd.ImmediateSending.ShouldBeFalse();
        cmd.SendingTimeUtc.ShouldBeNull();

        cmd.ImmediateSending = true;
        cmd.SendingTimeUtc = CK.Core.Util.UtcMinValue;
        cmd.ImmediateSending.ShouldBeFalse();
        cmd.SendingTimeUtc.ShouldBeNull();

        Util.Invokable( () => cmd.SendingTimeUtc = DateTime.Now ).ShouldThrow<ArgumentException>();
    }

    public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
    {
        public DHost()
        {
        }

        public DHost( string hostName )
            : base( hostName )
        {
        }
    }

    public class DConfiguration : DeviceConfiguration
    {
        public DConfiguration()
        {
        }

        public int ExecTimeMS { get; set; }

        public DConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte();
            ExecTimeMS = r.ReadInt32();
        }

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
                get.Completion.SetResult( (ReminderCount, ReminderFiredCount) );
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

        protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling )
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

    public class GetReminderCountCommand : DeviceCommand<DHost, (int, int)>
    {
    }

    public readonly struct Safe
    {
        readonly Func<string, Task> _action;
        readonly string _name;
        readonly DateTime _start;

        public Safe( Func<string, Task> a, [CallerMemberName] string? name = null )
        {
            _action = a;
            _name = name!;
            _start = DateTime.UtcNow;
        }

        public async Task RunAsync( params object[] parameters )
        {
            var mName = $"{_name}({parameters.Select( p => p.ToString() ).Concatenate()})";
            using var g = TestHelper.Monitor.OpenInfo( mName );
            GC.Collect();
            try
            {
                await _action( mName );
                GC.Collect();
            }
            catch( Exception ex )
            {
                GC.Collect();
                TestHelper.Monitor.Fatal( ex );
                throw;
            }
            finally
            {
                TestHelper.Monitor.CloseGroup( $"{mName} ended in {DateTime.UtcNow - _start}." );
            }
        }

    }

    [TestCase( 30, 200, 20 )]
    [TestCase( 50, 150, 20 )]
    [CancelAfter( 90000 )]
    public async Task SendingTimeUtc_stress_test_Async( int nb, int sendingDeltaMS, int execTimeMS, CancellationToken cancellation )
    {
        await new Safe( async testName =>
        {
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
                foreach( var cmd in commands )
                {
                    device.UnsafeSendCommand( TestHelper.Monitor, cmd ).ShouldBeTrue();
                }
            }

            var h = new DHost( testName );
            var config = new DConfiguration()
            {
                Name = "Single",
                Status = DeviceConfigurationStatus.RunnableStarted,
                ExecTimeMS = execTimeMS
            };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["Single"];
            Debug.Assert( d != null );

            var all = new List<DCommand>();
            SendCommands( d, false, all );
            SendCommands( d, true, all );
            SendCommands( d, null, all );

            TestHelper.Monitor.Info( $"Wait for the {all.Count} commands to be completed." );

            foreach( var c in all ) await c.Completion;

            // Each command triggers a 50 ms reminder.
            // A 30 ms margin should be enough for the reminders to complete.
            TestHelper.Monitor.Info( $"Wait for 50 ms reminder + 30 ms (safety)." );

            await Task.Delay( 50 + 30 );

            int rc = d.ReminderCount;
            int fc = d.ReminderFiredCount;
            if( rc != nb * 3 || fc != nb * 3 )
            {
                TestHelper.Monitor.Error( $"Failed: Final ReminderCount = {rc}, ReminderFiredCount = {fc} (should be both {nb * 3})." );
            }
            else
            {
                TestHelper.Monitor.Info( $"Success: Final ReminderCount = {rc}, ReminderFiredCount = {fc}." );
            }

            await h.ClearAsync( TestHelper.Monitor, true );

            rc.ShouldBe( nb * 3, "ReminderCount is fine." );
            fc.ShouldBe( nb * 3, "ReminderFiredCount is fine." );

        } ).RunAsync( nb, sendingDeltaMS, execTimeMS );
    }

    [Test]
    public async Task sendig_time_in_more_than_49_days_simply_warns_Async()
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "D", ExecTimeMS = 0, Status = DeviceConfigurationStatus.RunnableStarted };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        D? d = h["D"];
        Debug.Assert( d != null );

        var tooLate = DateTime.UtcNow.AddDays( 50 );

        var cToolate = new DCommand() { SendingTimeUtc = tooLate };

        // The adjustment is done when the command enters the delayed queue.
        cToolate.SendingTimeUtc.ShouldBe( tooLate );

        d.SendCommand( TestHelper.Monitor, cToolate, checkDeviceName: false );
        await d.WaitForSynchronizationAsync( false );

        // When the command is delayed and its time overflows, we update the SendingTimeUtc.
        // We can use this to check the adjustment.
        cToolate.SendingTimeUtc.ShouldNotBeNull().ShouldBeLessThan( tooLate );

        await h.ClearAsync( TestHelper.Monitor, true );

    }
}
