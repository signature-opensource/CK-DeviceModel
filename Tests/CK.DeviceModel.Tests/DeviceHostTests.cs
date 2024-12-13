using NUnit.Framework;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CK.Core;
using System.Diagnostics;
using FluentAssertions.Execution;
using static CK.Testing.MonitorTestHelper;
using System.Threading;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class DeviceHostTests
{

    [Test]
    public async Task playing_with_configurations_Async()
    {
        using var _ = TestHelper.Monitor.OpenInfo( nameof( playing_with_configurations_Async ) );

        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;
        FlashBulb.OnCommandComplededCount = 0;

        var config1 = new FlashBulbConfiguration() { Name = "First" };
        var config2 = new FlashBulbConfiguration { Name = "Another", Status = DeviceConfigurationStatus.Runnable };
        var config3 = new FlashBulbConfiguration { Name = "YetAnother", Status = DeviceConfigurationStatus.RunnableStarted };

        var host = new FlashBulbHost();
        host.Count.Should().Be( 0 );

        var hostConfig = new DeviceHostConfiguration<FlashBulbConfiguration>();
        hostConfig.IsPartialConfiguration.Should().BeTrue( "By default a configuration is partial." );

        hostConfig.Items.Add( config1 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.Should().Be( 1 );
        FlashBulb.TotalCount.Should().Be( 1 );
        FlashBulb? c1 = host.Find( "First" );
        Debug.Assert( c1 != null );
        c1.Name.Should().Be( "First" );
        c1.Status.IsRunning.Should().Be( false );
        // The real configuration is a clone.
        c1.ExternalConfiguration.Should().BeSameAs( config1 );
        // The external configuration has been validated.
        ((IFlashBulbConfiguration)config1).ComputedValid.Should().NotBeNull();

        hostConfig.Items.Add( config2 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.Should().Be( 2 );
        FlashBulb.TotalCount.Should().Be( 2 );
        FlashBulb.TotalRunning.Should().Be( 0 );
        FlashBulb? c2 = host.Find( "Another" );
        Debug.Assert( c2 != null );
        c2.Name.Should().Be( "Another" );
        c2.Status.IsRunning.Should().Be( false );
        c2.ExternalConfiguration.Should().BeSameAs( config2 );
        ((IFlashBulbConfiguration)config2).ComputedValid.Should().NotBeNull();

        hostConfig.Items.Add( config3 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.Should().Be( 3 );
        FlashBulb.TotalCount.Should().Be( 3 );
        FlashBulb.TotalRunning.Should().Be( 1 );
        FlashBulb? c3 = host.Find( "YetAnother" );
        Debug.Assert( c3 != null );
        c3.Name.Should().Be( "YetAnother" );
        c3.Status.IsRunning.Should().Be( true );
        c3.ExternalConfiguration.Should().BeSameAs( config3 );
        ((IFlashBulbConfiguration)config3).ComputedValid.Should().NotBeNull();

        host.Find( "Not here" ).Should().BeNull();

        (await c3.StopAsync( TestHelper.Monitor )).Should().BeTrue();
        FlashBulb.TotalRunning.Should().Be( 0 );
        c3.IsRunning.Should().BeFalse();

        // Partial configuration here: leave only config2 (RunnableStarted).
        hostConfig.Items.Remove( config3 );
        hostConfig.Items.Remove( config1 );

        config2.Status = DeviceConfigurationStatus.AlwaysRunning;

        c2.ExternalConfiguration.Should().BeSameAs( config2 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.Should().Be( 3 );
        c2.ExternalConfiguration.Should().BeSameAs( config2 );

        c1.IsRunning.Should().Be( false );
        c2.IsRunning.Should().Be( true );
        c3.IsRunning.Should().Be( false );

        hostConfig.IsPartialConfiguration = false;
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.Should().Be( 1 );

        host.Find( "First" ).Should().BeNull();
        host.Find( "Another" ).Should().BeSameAs( c2 );
        host.Find( "YetAnother" ).Should().BeNull();

        c2.ExternalConfiguration.Should().BeSameAs( config2 );
        var newConfig2 = new FlashBulbConfiguration() { Name = c2.Name, Status = DeviceConfigurationStatus.Disabled };
        ((IFlashBulbConfiguration)newConfig2).ComputedValid.Should().BeNull( "CheckValid has not been called." );
        await c2.ReconfigureAsync( TestHelper.Monitor, newConfig2 );
        c2.IsRunning.Should().BeFalse();
        c2.ExternalConfiguration.Should().NotBeSameAs( config2 ).And.BeSameAs( newConfig2 );
        ((IFlashBulbConfiguration)newConfig2).ComputedValid.Should().NotBeNull( "CheckValid has been called." );

        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        FlashBulb.TotalCount.Should().Be( 0 );
        FlashBulb.TotalRunning.Should().Be( 0 );
        FlashBulb.OnCommandComplededCount.Should().Be( 0, "Basic command (Start/Stop/Configure/Destroy) don't call OnCommandCompletedAsync." );

    }

    [Test]
    [CancelAfter( 200 )]
    public async Task testing_state_changed_PerfectEvent_Async( CancellationToken cancellation )
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( testing_state_changed_PerfectEvent_Async ) );

        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;

        int devicesCalled = 0;
        var lifetimeEvents = new System.Collections.Generic.List<DeviceLifetimeEvent>();

        var host = new FlashBulbHost();
        host.DevicesChanged.Sync += ( monitor, host, devices ) => ++devicesCalled;

        var config = new FlashBulbConfiguration() { Name = "C" };
        var hostConfig = new DeviceHostConfiguration<FlashBulbConfiguration>();
        hostConfig.Items.Add( config );

        var result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        result.Success.Should().BeTrue();
        result.HostConfiguration.Should().BeSameAs( hostConfig );
        result.Results![0].Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

        devicesCalled.Should().Be( 1 );

        var cameraC = host["C"];
        Debug.Assert( cameraC != null );
        {
            var status = cameraC.Status;
            status.HasStarted.Should().BeFalse();
            status.HasBeenReconfigured.Should().BeFalse();
            status.HasStopped.Should().BeFalse();
            status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.None );
            status.StartedReason.Should().Be( DeviceStartedReason.None );
            status.StoppedReason.Should().Be( DeviceStoppedReason.None );
            status.ToString().Should().Be( "Stopped (None)" );
        }
        cameraC.LifetimeEvent.Sync += ( m, e ) => lifetimeEvents.Add( e );

        var resultNoChange = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        resultNoChange.Success.Should().BeTrue();
        resultNoChange.Results![0].Should().Be( DeviceApplyConfigurationResult.None );

        devicesCalled.Should().Be( 1, "Still 1: no event raised." );
        lifetimeEvents.Should().BeEmpty( "None doesn't raise." );
        cameraC.Status.ToString().Should().Be( "Stopped (None)" );

        // Applying a new configuration.
        config.FlashColor = 1;
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        result.Success.Should().BeTrue();
        result.Results![0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

        devicesCalled.Should().Be( 1, "No new or destroyed devices." );
        lifetimeEvents.Should().HaveCount( 1, "Reconfiguration emits only one final event." );
        lifetimeEvents[0].StatusChanged.Should().BeTrue();
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.Should().BeFalse();
            status.HasBeenReconfigured.Should().BeTrue();
            status.HasStopped.Should().BeFalse();
            status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
            status.StartedReason.Should().Be( DeviceStartedReason.None );
            status.StoppedReason.Should().Be( DeviceStoppedReason.None );
            status.ToString().Should().Be( "Stopped (UpdateSucceeded)" );
        }
        lifetimeEvents[0].ConfigurationChanged.Should().BeTrue();
        {
            var c = lifetimeEvents[0].Configuration;
            c.Should().BeSameAs( config );
        }
        lifetimeEvents.Clear();

        // Try to start...
        (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeFalse( "Disabled." );
        cameraC.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
        lifetimeEvents.Should().BeEmpty();

        // No change.
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        lifetimeEvents.Should().BeEmpty();
        devicesCalled.Should().Be( 1 );

        // Changes the Configuration status. Nothing change except this Device.ConfigurationStatus...
        config.Status = DeviceConfigurationStatus.Runnable;
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        result.Results[0].Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
        lifetimeEvents.Should().HaveCount( 1 );
        lifetimeEvents[0].ConfigurationChanged.Should().BeTrue();
        cameraC.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.Runnable, "The Status has been updated." );
        devicesCalled.Should().Be( 1 );

        lifetimeEvents.Clear();
        // Starting the camera triggers a StatusChanged event.
        (await cameraC.StartAsync( TestHelper.Monitor )).Should().BeTrue();
        lifetimeEvents.Should().HaveCount( 1 );
        lifetimeEvents[0].StatusChanged.Should().BeTrue();
        lifetimeEvents[0].ConfigurationChanged.Should().BeFalse();
        lifetimeEvents[0].ControllerKeyChanged.Should().BeFalse();

        lifetimeEvents.Should().HaveCount( 1 );
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.Should().BeTrue();
            status.HasBeenReconfigured.Should().BeFalse();
            status.HasStopped.Should().BeFalse();
            status.StartedReason.Should().Be( DeviceStartedReason.StartCall );
            status.ToString().Should().Be( "Running (StartCall)" );
        }

        lifetimeEvents.Clear();
        // AutoDestroying by sending the command to host.
        var cmd = new DestroyDeviceCommand<FlashBulbHost>() { DeviceName = "C" };
        host.SendCommand( TestHelper.Monitor, cmd, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );
        await cmd.Completion.Task;

        devicesCalled.Should().Be( 2, "Device removed!" );
        host.Find( "C" ).Should().BeNull();
        lifetimeEvents.Should().HaveCount( 1 );
        lifetimeEvents[0].StatusChanged.Should().BeTrue();
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.Should().BeFalse();
            status.HasBeenReconfigured.Should().BeFalse();
            status.HasStopped.Should().BeTrue();
            status.StoppedReason.Should().Be( DeviceStoppedReason.Destroyed );
            status.ToString().Should().Be( "Stopped (Destroyed)" );
        }

        FlashBulb.TotalCount.Should().Be( 0 );
        FlashBulb.TotalRunning.Should().Be( 0 );
    }


    [Test]
    [CancelAfter( 200 )]
    public async Task ensure_device_Async( CancellationToken cancellation )
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( ensure_device_Async ) );

        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;

        var host = new FlashBulbHost();
        var d = host.Find( "n°1" );
        d.Should().BeNull();

        var config = new FlashBulbConfiguration()
        {
            Name = "n°1",
            FlashColor = 78
        };
        DeviceApplyConfigurationResult reconfigResult = await host.EnsureDeviceAsync( TestHelper.Monitor, config );
        reconfigResult.Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

        d = host.Find( "n°1" );
        Debug.Assert( d != null );
        d.Status.IsRunning.Should().BeFalse();
        d.Status.HasStopped.Should().BeFalse( "The device is not running... but it has not been stopped." );
        d.Status.StoppedReason.Should().Be( DeviceStoppedReason.None );
        d.Status.IsDestroyed.Should().BeFalse();
        d.Status.HasBeenReconfigured.Should().BeFalse();
        d.Status.HasStarted.Should().BeFalse();
        d.Status.StartedReason.Should().Be( DeviceStartedReason.None );
        d.Status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.None );

        config.Status = DeviceConfigurationStatus.AlwaysRunning;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

        d.ExternalConfiguration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
        d.Status.IsRunning.Should().BeTrue();
        d.Status.StartedReason.Should().Be( DeviceStartedReason.StartedByAlwaysRunningConfiguration );

        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.None, "No change: the Camera detects it." );

        config.ControllerKey = "Control";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded, "Even if the specific configuration did not change, changing the ControllerKey is a change." );

        d.Status.StartedReason.Should().Be( DeviceStartedReason.None );
        d.Status.HasBeenReconfigured.Should().BeTrue();
        d.Status.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.UpdateSucceeded );
        d.ControllerKey.Should().Be( "Control" );

        config.Status = DeviceConfigurationStatus.Disabled;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
        d.Status.HasStopped.Should().BeTrue();
        d.Status.StoppedReason.Should().Be( DeviceStoppedReason.StoppedByDisabledConfiguration );

        await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.Should().Be( 0 );
        FlashBulb.TotalRunning.Should().Be( 0 );

        await d.Awaiting( _ => _.DestroyAsync( TestHelper.Monitor ) ).Should().NotThrowAsync();
    }

    [Test]
    [CancelAfter( 200 )]
    public async Task executing_commands_from_the_host_without_ControllerKey_Async( CancellationToken cancellation )
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( executing_commands_from_the_host_without_ControllerKey_Async ) );

        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;

        var host = new FlashBulbHost();
        var config = new FlashBulbConfiguration()
        {
            Name = "n°1",
            FlashColor = 78,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d = host.Find( "n°1" );
        Debug.Assert( d != null );

        int flashLastColor = 0;
        d.TestFlash.Sync += ( m, c, color ) => flashLastColor = color;

        var cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
        host.SendCommand( TestHelper.Monitor, cmdF, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );
        await cmdF.Completion.Task;

        flashLastColor.Should().Be( 78 );

        var cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );
        await cmdS.Completion.Task;

        flashLastColor.Should().Be( 78 );
        cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
        host.SendCommand( TestHelper.Monitor, cmdF, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );
        await cmdF.Completion.Task;

        flashLastColor.Should().Be( 3712 );

        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).Should().Be( DeviceHostCommandResult.CommandCheckValidityFailed );

        cmdS = new SetFlashColorCommand() { DeviceName = "Not the 1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).Should().Be( DeviceHostCommandResult.DeviceNameNotFound );

        await d.SetControllerKeyAsync( TestHelper.Monitor, null, "The controlling key." );
        cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Controller key will fail!", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );
        await FluentActions.Awaiting( () => cmdS.Completion.Task ).Should().ThrowAsync<InvalidControllerKeyException>();

        cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "The controlling key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).Should().Be( DeviceHostCommandResult.Success );

        await cmdS.Completion.Task;

        await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.Should().Be( 0 );
        FlashBulb.TotalRunning.Should().Be( 0 );

    }

    [TestCase( "UseSendCommand" )]
    [TestCase( "UseSendCommandImmediate" )]
    [CancelAfter( 1000 )]
    public async Task sending_commands_checks_DeviceName_and_executing_checks_ControllerKey_Async( string mode, CancellationToken cancellation )
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( sending_commands_checks_DeviceName_and_executing_checks_ControllerKey_Async )}(\"{mode}\")" );

        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;
        FlashBulb.OnCommandComplededCount = 0;

        var host = new FlashBulbHost();
        var config = new FlashBulbConfiguration()
        {
            Name = "n°1",
            FlashColor = 78,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        FlashBulb? d = host.Find( "n°1" );
        Debug.Assert( d != null );

        bool SendCommand( BaseDeviceCommand c, bool checkDeviceName = true, bool checkControllerKey = true )
        {
            c.ImmediateSending = mode == "UseSendCommandImmediate";
            return d.SendCommand( TestHelper.Monitor, c, checkDeviceName, checkControllerKey, cancellation );
        }

        int flashLastColor = 0;
        d.TestFlash.Sync += ( m, c, color ) => flashLastColor = color;

        var cmdSet = new SetFlashColorCommand()
        {
            DeviceName = "n°1",
            ControllerKey = "Never mind since the device's ControllerKey is null.",
            Color = 6
        };
        var cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1" };

        SendCommand( cmdSet );
        SendCommand( cmdRaiseFlash );

        (await cmdSet.Completion).Should().Be( 78 );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.Should().Be( 6 );
        // Completion is signaled and then OnCommandComplededAsyc is called.
        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.Should().Be( 2 );

        // Use the basic command to set a ControllerKey.
        var setControllerKey = new SetControllerKeyDeviceCommand<FlashBulbHost>()
        {
            ControllerKey = "Never mind since the device's ControllerKey is null.",
            NewControllerKey = "I'm controlling.",
            DeviceName = "n°1"
        };
        SendCommand( setControllerKey );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm not in charge. Completion will throw an InvalidControllerKeyException." };
        SendCommand( cmdSet ).Should().BeTrue();

        do
        {
            await Task.Delay( 100, cancellation );
        }
        while( !cmdSet.Completion.IsCompleted );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling.", Color = 18 };
        SendCommand( cmdSet );

        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1" };
        SendCommand( cmdRaiseFlash ).Should().BeTrue();
        await FluentActions.Awaiting( () => cmdRaiseFlash.Completion.Task ).Should().ThrowAsync<InvalidControllerKeyException>();

        flashLastColor.Should().Be( 6 );
        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.Should().Be( 5 );

        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling." };
        SendCommand( cmdRaiseFlash );

        await cmdRaiseFlash.Completion.Task;
        flashLastColor.Should().Be( 18 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.Should().Be( 6 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling, but checkControllerKey: false is used.", Color = 1 };
        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling too." };
        SendCommand( cmdSet, checkControllerKey: false );
        SendCommand( cmdRaiseFlash, checkControllerKey: false );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.Should().Be( 1 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.Should().Be( 8 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling.", Color = 1 };
        FluentActions.Invoking( () => SendCommand( cmdSet ) ).Should().Throw<ArgumentException>();

        cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling." };
        cmdRaiseFlash.DeviceName = "Not the right device name: this will throw an ArgumentException.";
        FluentActions.Invoking( () => SendCommand( cmdRaiseFlash ) ).Should().Throw<ArgumentException>();

        FlashBulb.OnCommandComplededCount.Should().Be( 8 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name but checkDeviceName: false is used.", ControllerKey = "I'm controlling.", Color = 3712 };
        cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name too.", ControllerKey = "I'm controlling." };
        SendCommand( cmdSet, checkDeviceName: false );
        SendCommand( cmdRaiseFlash, checkDeviceName: false );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.Should().Be( 3712 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.Should().Be( 10 );

        await d.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.Should().Be( 0 );
        FlashBulb.TotalRunning.Should().Be( 0 );

        FlashBulb.OnCommandComplededCount.Should().Be( 10 );

    }

    [Test]
    public async Task Disabling_sends_a_stop_status_change_Async()
    {
        using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( Disabling_sends_a_stop_status_change_Async ) );
        var host = new MachineHost();

        var config = new MachineConfiguration()
        {
            Name = "Test",
            Status = DeviceConfigurationStatus.AlwaysRunning
        };

        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var device = host["Test"];
        Debug.Assert( device != null );

        bool stopReceived = false;
        bool destroyReceived = false;

        device.LifetimeEvent.Sync += ( monitor, e ) =>
        {
            monitor.Info( $"Received LifetimeEvent. DeviceStatus = {e.DeviceStatus}." );
            if( e.StatusChanged )
            {
                // The device's status is up to date.
                Debug.Assert( e.Device.Status == e.DeviceStatus );
                TestHelper.Monitor.Info( $"Status change." );
                if( e.DeviceStatus.IsDestroyed )
                {
                    destroyReceived.Should().BeFalse();
                    destroyReceived = true;
                }
                else if( e.DeviceStatus.HasStopped )
                {
                    // HasStopped is true when IsDestroyed is sent.
                    stopReceived.Should().BeFalse();
                    stopReceived = true;
                }
            }
        };

        using( TestHelper.Monitor.OpenInfo( "Reconfiguring to Disabled." ).ConcludeWith( () => "Reconfigured to Disabled." ) )
        {
            config.Status = DeviceConfigurationStatus.Disabled;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
        }
        stopReceived.Should().BeTrue();
        destroyReceived.Should().BeFalse();

        await device.DestroyAsync( TestHelper.Monitor );
        destroyReceived.Should().BeTrue();

        TestHelper.Monitor.Info( "/Disabling_sends_a_stop_status_change_Async" );
    }

}
