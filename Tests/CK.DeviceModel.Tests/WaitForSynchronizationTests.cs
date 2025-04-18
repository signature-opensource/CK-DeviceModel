using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class WaitForSynchronizationTests
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
            if( command is DCommand cmd )
            {
                await Task.Delay( cmd.ExecutionTime, cmd.CancellationToken ).ConfigureAwait( false );
                cmd.Completion.SetResult();
                return;
            }
            await base.DoHandleCommandAsync( monitor, command );
        }
    }

    public class DCommand : DeviceCommand<DHost>
    {
        public int ExecutionTime { get; set; }

        protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.AlwaysWaitForNextStart;
    }

    [Test]
    public async Task cancellation_and_timeout_are_handled_Async()
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "D", Status = DeviceConfigurationStatus.RunnableStarted };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        D? d = h["D"];
        Debug.Assert( d != null );

        var start = DateTime.UtcNow;
        d.SendCommand( TestHelper.Monitor, NewCommand() );
        var result = await d.WaitForSynchronizationAsync( considerDeferredCommands: false );
        (DateTime.UtcNow - start).ShouldBeGreaterThan( TimeSpan.FromMilliseconds( 100 - 20 ) );
        result.ShouldBe( WaitForSynchronizationResult.Success );

        d.SendCommand( TestHelper.Monitor, NewCommand() );
        result = await d.WaitForSynchronizationAsync( considerDeferredCommands: false, timeout: 20 );
        result.ShouldBe( WaitForSynchronizationResult.Timeout );

        CancellationTokenSource cts = new CancellationTokenSource();
        d.SendCommand( TestHelper.Monitor, NewCommand() );
        await cts.CancelAsync();
        result = await d.WaitForSynchronizationAsync( considerDeferredCommands: false, cancel: cts.Token );
        result.ShouldBe( WaitForSynchronizationResult.Canceled );

        CancellationTokenSource ctsTimed = new CancellationTokenSource( 20 );
        result = await d.WaitForSynchronizationAsync( considerDeferredCommands: false, cancel: ctsTimed.Token );
        result.ShouldBe( WaitForSynchronizationResult.Canceled );

        static DCommand NewCommand()
        {
            return new DCommand()
            {
                DeviceName = "D",
                ExecutionTime = 100
            };
        }
    }

    [Test]
    public async Task deferred_commands_can_be_awaited_Async()
    {
        var h = new DHost();
        var config = new DConfiguration() { Name = "D", Status = DeviceConfigurationStatus.Runnable };
        (await h.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );
        D? d = h["D"];
        Debug.Assert( d != null );

        // Device is stopped, StoppedBehavior = AlwaysWaitForNextStart => deferred.
        d.SendCommand( TestHelper.Monitor, NewCommand() );
        var taskResult = d.WaitForSynchronizationAsync( considerDeferredCommands: true );

        // Wait for the command execution time just to be sure!
        await Task.Delay( 100 );

        // No completion yet.
        taskResult.IsCompleted.ShouldBeFalse();

        // Starts the device. The deferred command will be handled.
        await d.StartAsync( TestHelper.Monitor );

        // Still no completion: the command is executing.
        await Task.Delay( 50 );
        taskResult.IsCompleted.ShouldBeFalse();

        // After 100 ms (+20 ms for security), the command must have been handled: the WaitForSynchronizationAsync should be resolved.
        await Task.Delay( 50 + 20 );
        taskResult.IsCompleted.ShouldBeTrue();

        static DCommand NewCommand()
        {
            return new DCommand()
            {
                DeviceName = "D",
                ExecutionTime = 100
            };
        }
    }


}
