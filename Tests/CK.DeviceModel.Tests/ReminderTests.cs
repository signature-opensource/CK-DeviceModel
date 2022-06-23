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
    public class ReminderTests
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
            }

            public override void Write( ICKBinaryWriter w )
            {
                base.Write( w );
            }
        }

        public class D : Device<DConfiguration>
        {
            public D( IActivityMonitor monitor, CreateInfo info )
                : base( monitor, info )
            {
            }

            protected override Task DoDestroyAsync( IActivityMonitor monitor ) => Task.CompletedTask;
            protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, DConfiguration config ) => Task.FromResult( DeviceReconfiguredResult.None );
            protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason ) => Task.FromResult( true );
            protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason ) => Task.CompletedTask;
            protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
            {
                if( command is AddManyRemindersCommand c1 )
                {
                    for( int i = 0; i < c1.Count; i++ )
                    {
                        AddReminder( TimeSpan.FromMilliseconds( 60 + Random.Shared.Next( 1000 ) ), null );
                    }
                    c1.Completion.SetResult();
                    return;
                }
                else if( command is AddReminderIn50DaysCommand c2 )
                {
                    // This will pass!
                    if( c2.In49Days ) AddReminder( TimeSpan.FromDays( 49 ), null );
                    else AddReminder( TimeSpan.FromDays( 50 ), null ); // This will throw!
                    c2.Completion.SetResult();
                    return;
                }
                else if( command is AddRemindersInPast c3 )
                {
                    AddReminder( DateTime.UtcNow, "was now..." );
                    AddReminder( DateTime.UtcNow.AddDays( -1 ), "was yesterday..." );
                    c3.Completion.SetResult();
                    return;
                }
                await base.DoHandleCommandAsync( monitor, command );
            }

            public int NumberOfRemindersInPast;

            protected override Task OnReminderAsync( IActivityMonitor monitor, DateTime reminderTimeUtc, object? state, bool immediateHandling )
            {
                bool inPast = state is string s && s.StartsWith( "was " );
                Debug.Assert( immediateHandling == inPast );
                if( inPast ) NumberOfRemindersInPast++;
                return Task.CompletedTask;
            }
        }


        public class AddManyRemindersCommand : DeviceCommand<DHost>
        {
            public int Count { get; set; }
        }

        public class AddReminderIn50DaysCommand : DeviceCommand<DHost>
        {
            public bool In49Days;
        }

        public class AddRemindersInPast : DeviceCommand<DHost>
        {
        }

        [Test]
        public async Task pooled_reminders_are_released_when_the_device_is_destroyed_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( pooled_reminders_are_released_when_the_device_is_destroyed_Async )}" );

            var h = new DHost();
            var config = new DConfiguration() { Name = "D", Status = DeviceConfigurationStatus.RunnableStarted };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["D"];
            Debug.Assert( d != null );

            var c = new AddManyRemindersCommand() { Count = D.MaxPooledReminderPerDevice + 10 };
            d.SendCommand( TestHelper.Monitor, c, checkDeviceName: false );
            await c.Completion.Task;

            D.ReminderPoolTotalCount.Should().Be( D.MaxPooledReminderPerDevice );
            D.ReminderPoolInUseCount.Should().Be( D.MaxPooledReminderPerDevice );

            await d.DestroyAsync( TestHelper.Monitor, true );
            TestHelper.Monitor.Info( "await d.DestroyAsync done." );

            D.ReminderPoolTotalCount.Should().Be( D.MaxPooledReminderPerDevice );
            D.ReminderPoolInUseCount.Should().Be( 0 );
        }

        [Test]
        public async Task reminders_cannot_exceed_49_days_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( reminders_cannot_exceed_49_days_Async )}" );

            var h = new DHost();
            var config = new DConfiguration() { Name = "D", Status = DeviceConfigurationStatus.RunnableStarted };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["D"];
            Debug.Assert( d != null );

            var cTooMuch = new AddReminderIn50DaysCommand();
            d.SendCommand( TestHelper.Monitor, cTooMuch, checkDeviceName: false );
            await FluentActions.Awaiting( () => cTooMuch.Completion.Task ).Should().ThrowAsync<NotSupportedException>();

            // Restarts the device (the error stopped it).
            await d.StartAsync( TestHelper.Monitor );

            var cPass = new AddReminderIn50DaysCommand() { In49Days = true };
            d.SendCommand( TestHelper.Monitor, cPass, checkDeviceName: false );
            await cPass.Completion.Task;

            await h.ClearAsync( TestHelper.Monitor, true );
        }

        [Test]
        public async Task reminders_now_or_in_past_are_sent_as_immediate_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( reminders_now_or_in_past_are_sent_as_immediate_Async )}" );

            var h = new DHost();
            var config = new DConfiguration() { Name = "D", Status = DeviceConfigurationStatus.RunnableStarted };
            (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            D? d = h["D"];
            Debug.Assert( d != null );

            d.NumberOfRemindersInPast.Should().Be( 0 );

            var inPast = new AddRemindersInPast();
            d.SendCommand( TestHelper.Monitor, inPast, checkDeviceName: false );

            // The command succeeded.
            await inPast.Completion.Task;

            await d.WaitForSynchronizationAsync( false );

            // And the device is still alive.
            d.Status.IsRunning.Should().BeTrue();
            // And we have seen the 2 reminders handled as immediate.
            d.NumberOfRemindersInPast.Should().Be( 2 );

            await h.ClearAsync( TestHelper.Monitor, true );
        }

    }
}
