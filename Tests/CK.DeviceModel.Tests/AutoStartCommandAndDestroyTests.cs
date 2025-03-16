using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class AutoStartCommandAndDestroyTests
{
    public class DHost : DeviceHost<D, DeviceHostConfiguration<DConfiguration>, DConfiguration>
    {
    }

    public class DConfiguration : DeviceConfiguration
    {
        public DConfiguration()
        {
        }

        public string? Trace { get; set; }

        public DConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            Trace = r.ReadNullableString();
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.WriteNullableString( Trace );
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

        protected override async Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            if( command is DCommand cmd )
            {
                await Task.Delay( 10, cmd.CancellationToken ).ConfigureAwait( false );
                Traces.Add( $"Command {cmd.Trace}" );
                monitor.Info( $"Handling {cmd.Trace}" );
                cmd.Completion.SetResult();
                return;
            }
            await base.DoHandleCommandAsync( monitor, command );
        }
    }


    public class DCommand : DeviceCommand<DHost>
    {
        public string? Trace { get; set; }

        protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.AlwaysWaitForNextStart;

        protected override string? ToStringSuffix => Trace;
    }

    public class DCommandStarter : DCommand
    {
        readonly DeviceCommandStoppedBehavior _stoppedBehavior;
        public DCommandStarter( bool keepDeviceRunning )
        {
            _stoppedBehavior = keepDeviceRunning ? DeviceCommandStoppedBehavior.AutoStartAndKeepRunning : DeviceCommandStoppedBehavior.SilentAutoStartAndStop;
        }

        protected override DeviceCommandStoppedBehavior StoppedBehavior => _stoppedBehavior;
    }

    [Test]
    [CancelAfter( 500 )]
    public async Task auto_starting_device_and_keep_running_eventually_execute_deferred_commands_Async( CancellationToken cancellation )
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );
        D? d = h["First"];
        Debug.Assert( d != null );

        bool statusChanged = false;
        void OnLifetimeChange( IActivityMonitor monitor, DeviceLifetimeEvent e ) => statusChanged |= e.StatusChanged;
        d.LifetimeEvent.Sync += OnLifetimeChange;

        var commands = Enumerable.Range( 0, 3 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
        var destroy = new DestroyDeviceCommand<DHost>() { DeviceName = "First", ImmediateSending = false };

        foreach( var c in commands )
        {
            h.SendCommand( TestHelper.Monitor, c, token: cancellation );
        }
        h.SendCommand( TestHelper.Monitor, new DCommandStarter( keepDeviceRunning: true ) { DeviceName = "First", Trace = "STARTER!" }, token: cancellation );

        // Sending Destroy as a regular command (not an immediate one).
        h.SendCommand( TestHelper.Monitor, destroy, token: cancellation );

        await commands[0].Completion.Task;
        statusChanged.ShouldBeTrue( "Since the first deferred command is executed, the device has started." );

        await destroy.Completion;

        foreach( var c in commands )
        {
            c.Completion.IsCompleted.ShouldBeTrue();
            c.Completion.Task.IsCompletedSuccessfully.ShouldBeTrue();
        }

        d.Traces.ShouldBe( [ "Start StartAndKeepRunningStoppedBehavior",
                             "Command STARTER!",
                             "Command n°0", "Command n°1", "Command n°2",
                             "Stop Destroyed",
                             "Destroy"] );
    }

    [TestCase( "ExecuteDeferred" )]
    [TestCase( "DoNotExecuteDeferred" )]
    [CancelAfter( 500 )]
    public async Task auto_starting_device_and_stop_skip_deferred_and_no_event_is_raised_Async( string mode, CancellationToken cancellation )
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );
        D? d = h["First"];
        Debug.Assert( d != null );

        var initialStatus = d.Status.ToString();
        initialStatus.ShouldBe( "Stopped (None)" );

        bool statusChanged = false;
        void OnLifetimeChange( IActivityMonitor monitor, DeviceLifetimeEvent e ) => statusChanged |= e.StatusChanged;
        d.LifetimeEvent.Sync += OnLifetimeChange;

        var commands = Enumerable.Range( 0, 3 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
        var starter = new DCommandStarter( keepDeviceRunning: false ) { DeviceName = "First", Trace = "DO IT" };
        var destroy = new DestroyDeviceCommand<DHost>() { DeviceName = "First" };

        foreach( var c in commands )
        {
            h.SendCommand( TestHelper.Monitor, c, token: cancellation );
        }

        // Sends the auto start command and wait for its completion.
        h.SendCommand( TestHelper.Monitor, starter, token: cancellation );
        await starter.Completion.Task;

        // To let the implicit Stop command do its work (HandleCommandAutoStartAsync is called after the
        // DoHandleCommandAsync of the real command), we use the WaitForSynchronizationAsync.
        // Since the device is Stopped and there are deferred commands we must ignore them otherwise
        // we'll be waiting for the device to restart.
        (await d.WaitForSynchronizationAsync( considerDeferredCommands: false, cancel: cancellation )).ShouldBe( WaitForSynchronizationResult.Success );
        statusChanged.ShouldBeFalse( "No visible status change despite the fact that the device has start/stop." );
        d.Status.ToString().ShouldBe( initialStatus );

        bool executeDeferred = mode == "ExecuteDeferred";
        if( executeDeferred )
        {
            // We want to monitor the end of the currently deferred commands.
            var afterDeferred = d.WaitForSynchronizationAsync( considerDeferredCommands: true, cancel: cancellation );
            await d.StartAsync( TestHelper.Monitor );
            (await afterDeferred).ShouldBe( WaitForSynchronizationResult.Success );
            foreach( var c in commands )
            {
                c.Completion.Task.IsCompletedSuccessfully.ShouldBeTrue();
                c.Completion.HasSucceed.ShouldBeTrue();
            }
        }
        else
        {
            foreach( var c in commands )
            {
                c.Completion.Task.IsCompleted.ShouldBeFalse();
                c.Completion.IsCompleted.ShouldBeFalse();
            }
        }
        // Destroying the device.
        h.SendCommand( TestHelper.Monitor, destroy, token: cancellation );
        // Using WaitForSynchronizationAsync here may lead to Success or IsDetroyed.
        (await d.WaitForSynchronizationAsync( considerDeferredCommands: false, cancel: cancellation ))
            .ShouldMatch( r => r == WaitForSynchronizationResult.Success
                                    || r == WaitForSynchronizationResult.DeviceDestroyed );
        destroy.Completion.Task.IsCompleted.ShouldBeTrue();

        if( executeDeferred )
        {
            foreach( var c in commands )
            {
                c.Completion.Task.IsCompletedSuccessfully.ShouldBeTrue();
                c.Completion.HasSucceed.ShouldBeTrue();
            }
            d.Traces.ShouldBe( ["Start SilentAutoStartAndStopStoppedBehavior",
                                "Command DO IT",
                                "Stop SilentAutoStartAndStopStoppedBehavior",
                                "Start StartCall",
                                "Command n°0",
                                "Command n°1",
                                "Command n°2",
                                "Stop Destroyed",
                                "Destroy"] );
        }
        else
        {
            foreach( var c in commands )
            {
                c.Completion.Task.IsFaulted.ShouldBeTrue();
                c.Completion.HasFailed.ShouldBeTrue();
            }
            d.Traces.ShouldBe( [ "Start SilentAutoStartAndStopStoppedBehavior",
                                 "Command DO IT",
                                 "Stop SilentAutoStartAndStopStoppedBehavior",
                                 "Destroy" ] );
        }

    }

    [TestCase( "Deferred" )]
    [TestCase( "StartFirst" )]
    [CancelAfter( 500 )]
    public async Task destroying_the_device_eventually_set_the_UnavailableDeviceException_on_all_pending_commands_Async( string mode, CancellationToken cancellation )
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "First", Status = DeviceConfigurationStatus.Runnable };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );
        D? d = h["First"];
        Debug.Assert( d != null );

        var commands = Enumerable.Range( 0, 10 ).Select( i => new DCommand() { DeviceName = "First", Trace = $"n°{i}" } ).ToArray();
        if( mode == "StartFirst" ) (await d.StartAsync( TestHelper.Monitor )).ShouldBeTrue();
        foreach( var c in commands )
        {
            h.SendCommand( TestHelper.Monitor, c, token: cancellation );
        }
        if( mode != "StartFirst" ) (await d.StartAsync( TestHelper.Monitor )).ShouldBeTrue();

        // Immediately destroys the device.
        await d.DestroyAsync( TestHelper.Monitor );

        TestHelper.Monitor.Info( $"Current Completions: {commands.Select( cz => cz.Completion.ToString() ).Concatenate()}." );
        foreach( var c in commands )
        {
            try
            {
                await c.Completion;
                // This is expected.
            }
            catch( UnavailableDeviceException )
            {
                // This is expected.
            }
            c.Completion.IsCompleted.ShouldBeTrue();
        }

        commands.Any( c => c.Completion.HasFailed ).ShouldBeTrue( "At least one command should be true." );
    }


}
