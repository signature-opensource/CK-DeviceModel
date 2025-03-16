using NUnit.Framework;
using Shouldly;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CK.Core;
using System.Diagnostics;
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
        host.Count.ShouldBe( 0 );

        var hostConfig = new DeviceHostConfiguration<FlashBulbConfiguration>();
        hostConfig.IsPartialConfiguration.ShouldBeTrue( "By default a configuration is partial." );

        hostConfig.Items.Add( config1 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.ShouldBe( 1 );
        FlashBulb.TotalCount.ShouldBe( 1 );
        FlashBulb? c1 = host.Find( "First" );
        Debug.Assert( c1 != null );
        c1.Name.ShouldBe( "First" );
        c1.Status.IsRunning.ShouldBe( false );
        // The real configuration is a clone.
        c1.ExternalConfiguration.ShouldBeSameAs( config1 );
        // The external configuration has been validated.
        ((IFlashBulbConfiguration)config1).ComputedValid.ShouldNotBeNull();

        hostConfig.Items.Add( config2 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.ShouldBe( 2 );
        FlashBulb.TotalCount.ShouldBe( 2 );
        FlashBulb.TotalRunning.ShouldBe( 0 );
        FlashBulb? c2 = host.Find( "Another" );
        Debug.Assert( c2 != null );
        c2.Name.ShouldBe( "Another" );
        c2.Status.IsRunning.ShouldBe( false );
        c2.ExternalConfiguration.ShouldBeSameAs( config2 );
        ((IFlashBulbConfiguration)config2).ComputedValid.ShouldNotBeNull();

        hostConfig.Items.Add( config3 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.ShouldBe( 3 );
        FlashBulb.TotalCount.ShouldBe( 3 );
        FlashBulb.TotalRunning.ShouldBe( 1 );
        FlashBulb? c3 = host.Find( "YetAnother" );
        Debug.Assert( c3 != null );
        c3.Name.ShouldBe( "YetAnother" );
        c3.Status.IsRunning.ShouldBe( true );
        c3.ExternalConfiguration.ShouldBeSameAs( config3 );
        ((IFlashBulbConfiguration)config3).ComputedValid.ShouldNotBeNull();

        host.Find( "Not here" ).ShouldBeNull();

        (await c3.StopAsync( TestHelper.Monitor )).ShouldBeTrue();
        FlashBulb.TotalRunning.ShouldBe( 0 );
        c3.IsRunning.ShouldBeFalse();

        // Partial configuration here: leave only config2 (RunnableStarted).
        hostConfig.Items.Remove( config3 );
        hostConfig.Items.Remove( config1 );

        config2.Status = DeviceConfigurationStatus.AlwaysRunning;

        c2.ExternalConfiguration.ShouldBeSameAs( config2 );
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.ShouldBe( 3 );
        c2.ExternalConfiguration.ShouldBeSameAs( config2 );

        c1.IsRunning.ShouldBe( false );
        c2.IsRunning.ShouldBe( true );
        c3.IsRunning.ShouldBe( false );

        hostConfig.IsPartialConfiguration = false;
        await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        host.Count.ShouldBe( 1 );

        host.Find( "First" ).ShouldBeNull();
        host.Find( "Another" ).ShouldBeSameAs( c2 );
        host.Find( "YetAnother" ).ShouldBeNull();

        c2.ExternalConfiguration.ShouldBeSameAs( config2 );
        var newConfig2 = new FlashBulbConfiguration() { Name = c2.Name, Status = DeviceConfigurationStatus.Disabled };
        ((IFlashBulbConfiguration)newConfig2).ComputedValid.ShouldBeNull( "CheckValid has not been called." );
        await c2.ReconfigureAsync( TestHelper.Monitor, newConfig2 );
        c2.IsRunning.ShouldBeFalse();
        c2.ExternalConfiguration.ShouldNotBeSameAs( config2 ).ShouldBeSameAs( newConfig2 );
        ((IFlashBulbConfiguration)newConfig2).ComputedValid.ShouldNotBeNull( "CheckValid has been called." );

        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        FlashBulb.TotalCount.ShouldBe( 0 );
        FlashBulb.TotalRunning.ShouldBe( 0 );
        FlashBulb.OnCommandComplededCount.ShouldBe( 0, "Basic command (Start/Stop/Configure/Destroy) don't call OnCommandCompletedAsync." );

    }

    [Test]
    [CancelAfter( 200 )]
    public async Task testing_state_changed_PerfectEvent_Async( CancellationToken cancellation )
    {
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
        result.Success.ShouldBeTrue();
        result.HostConfiguration.ShouldBeSameAs( hostConfig );
        result.Results![0].ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );

        devicesCalled.ShouldBe( 1 );

        var cameraC = host["C"];
        Debug.Assert( cameraC != null );
        {
            var status = cameraC.Status;
            status.HasStarted.ShouldBeFalse();
            status.HasBeenReconfigured.ShouldBeFalse();
            status.HasStopped.ShouldBeFalse();
            status.ReconfiguredResult.ShouldBe( DeviceReconfiguredResult.None );
            status.StartedReason.ShouldBe( DeviceStartedReason.None );
            status.StoppedReason.ShouldBe( DeviceStoppedReason.None );
            status.ToString().ShouldBe( "Stopped (None)" );
        }
        cameraC.LifetimeEvent.Sync += ( m, e ) => lifetimeEvents.Add( e );

        var resultNoChange = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        resultNoChange.Success.ShouldBeTrue();
        resultNoChange.Results![0].ShouldBe( DeviceApplyConfigurationResult.None );

        devicesCalled.ShouldBe( 1, "Still 1: no event raised." );
        lifetimeEvents.ShouldBeEmpty( "None doesn't raise." );
        cameraC.Status.ToString().ShouldBe( "Stopped (None)" );

        // Applying a new configuration.
        config.FlashColor = 1;
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        result.Success.ShouldBeTrue();
        result.Results![0].ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );

        devicesCalled.ShouldBe( 1, "No new or destroyed devices." );
        lifetimeEvents.Count.ShouldBe( 1, "Reconfiguration emits only one final event." );
        lifetimeEvents[0].StatusChanged.ShouldBeTrue();
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.ShouldBeFalse();
            status.HasBeenReconfigured.ShouldBeTrue();
            status.HasStopped.ShouldBeFalse();
            status.ReconfiguredResult.ShouldBe( DeviceReconfiguredResult.UpdateSucceeded );
            status.StartedReason.ShouldBe( DeviceStartedReason.None );
            status.StoppedReason.ShouldBe( DeviceStoppedReason.None );
            status.ToString().ShouldBe( "Stopped (UpdateSucceeded)" );
        }
        lifetimeEvents[0].ConfigurationChanged.ShouldBeTrue();
        {
            var c = lifetimeEvents[0].Configuration;
            c.ShouldBeSameAs( config );
        }
        lifetimeEvents.Clear();

        // Try to start...
        (await cameraC.StartAsync( TestHelper.Monitor )).ShouldBeFalse( "Disabled." );
        cameraC.ExternalConfiguration.Status.ShouldBe( DeviceConfigurationStatus.Disabled );
        lifetimeEvents.ShouldBeEmpty();

        // No change.
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        lifetimeEvents.ShouldBeEmpty();
        devicesCalled.ShouldBe( 1 );

        // Changes the Configuration status. Nothing change except this Device.ConfigurationStatus...
        config.Status = DeviceConfigurationStatus.Runnable;
        result = await host.ApplyConfigurationAsync( TestHelper.Monitor, hostConfig );
        result.Results[0].ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );
        lifetimeEvents.Count.ShouldBe( 1 );
        lifetimeEvents[0].ConfigurationChanged.ShouldBeTrue();
        cameraC.ExternalConfiguration.Status.ShouldBe( DeviceConfigurationStatus.Runnable, "The Status has been updated." );
        devicesCalled.ShouldBe( 1 );

        lifetimeEvents.Clear();
        // Starting the camera triggers a StatusChanged event.
        (await cameraC.StartAsync( TestHelper.Monitor )).ShouldBeTrue();
        lifetimeEvents.Count.ShouldBe( 1 );
        lifetimeEvents[0].StatusChanged.ShouldBeTrue();
        lifetimeEvents[0].ConfigurationChanged.ShouldBeFalse();
        lifetimeEvents[0].ControllerKeyChanged.ShouldBeFalse();

        lifetimeEvents.Count.ShouldBe( 1 );
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.ShouldBeTrue();
            status.HasBeenReconfigured.ShouldBeFalse();
            status.HasStopped.ShouldBeFalse();
            status.StartedReason.ShouldBe( DeviceStartedReason.StartCall );
            status.ToString().ShouldBe( "Running (StartCall)" );
        }

        lifetimeEvents.Clear();
        // AutoDestroying by sending the command to host.
        var cmd = new DestroyDeviceCommand<FlashBulbHost>() { DeviceName = "C" };
        host.SendCommand( TestHelper.Monitor, cmd, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );
        await cmd.Completion.Task;

        devicesCalled.ShouldBe( 2, "Device removed!" );
        host.Find( "C" ).ShouldBeNull();
        lifetimeEvents.Count.ShouldBe( 1 );
        lifetimeEvents[0].StatusChanged.ShouldBeTrue();
        {
            var status = lifetimeEvents[0].DeviceStatus;
            status.HasStarted.ShouldBeFalse();
            status.HasBeenReconfigured.ShouldBeFalse();
            status.HasStopped.ShouldBeTrue();
            status.StoppedReason.ShouldBe( DeviceStoppedReason.Destroyed );
            status.ToString().ShouldBe( "Stopped (Destroyed)" );
        }

        FlashBulb.TotalCount.ShouldBe( 0 );
        FlashBulb.TotalRunning.ShouldBe( 0 );
    }


    [Test]
    [CancelAfter( 200 )]
    public async Task ensure_device_Async( CancellationToken cancellation )
    {
        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;

        var host = new FlashBulbHost();
        var d = host.Find( "n°1" );
        d.ShouldBeNull();

        var config = new FlashBulbConfiguration()
        {
            Name = "n°1",
            FlashColor = 78
        };
        DeviceApplyConfigurationResult reconfigResult = await host.EnsureDeviceAsync( TestHelper.Monitor, config );
        reconfigResult.ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );

        d = host.Find( "n°1" );
        Debug.Assert( d != null );
        d.Status.IsRunning.ShouldBeFalse();
        d.Status.HasStopped.ShouldBeFalse( "The device is not running... but it has not been stopped." );
        d.Status.StoppedReason.ShouldBe( DeviceStoppedReason.None );
        d.Status.IsDestroyed.ShouldBeFalse();
        d.Status.HasBeenReconfigured.ShouldBeFalse();
        d.Status.HasStarted.ShouldBeFalse();
        d.Status.StartedReason.ShouldBe( DeviceStartedReason.None );
        d.Status.ReconfiguredResult.ShouldBe( DeviceReconfiguredResult.None );

        config.Status = DeviceConfigurationStatus.AlwaysRunning;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );

        d.ExternalConfiguration.Status.ShouldBe( DeviceConfigurationStatus.AlwaysRunning );
        d.Status.IsRunning.ShouldBeTrue();
        d.Status.StartedReason.ShouldBe( DeviceStartedReason.StartedByAlwaysRunningConfiguration );

        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.None, "No change: the Camera detects it." );

        config.ControllerKey = "Control";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded, "Even if the specific configuration did not change, changing the ControllerKey is a change." );

        d.Status.StartedReason.ShouldBe( DeviceStartedReason.None );
        d.Status.HasBeenReconfigured.ShouldBeTrue();
        d.Status.ReconfiguredResult.ShouldBe( DeviceReconfiguredResult.UpdateSucceeded );
        d.ControllerKey.ShouldBe( "Control" );

        config.Status = DeviceConfigurationStatus.Disabled;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );
        d.Status.HasStopped.ShouldBeTrue();
        d.Status.StoppedReason.ShouldBe( DeviceStoppedReason.StoppedByDisabledConfiguration );

        await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.ShouldBe( 0 );
        FlashBulb.TotalRunning.ShouldBe( 0 );

        await Util.Awaitable( () => d.DestroyAsync( TestHelper.Monitor ) ).ShouldNotThrowAsync();
    }

    [Test]
    [CancelAfter( 200 )]
    public async Task executing_commands_from_the_host_without_ControllerKey_Async( CancellationToken cancellation )
    {
        FlashBulb.TotalCount = 0;
        FlashBulb.TotalRunning = 0;

        var host = new FlashBulbHost();
        var config = new FlashBulbConfiguration()
        {
            Name = "n°1",
            FlashColor = 78,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d = host.Find( "n°1" );
        Debug.Assert( d != null );

        int flashLastColor = 0;
        d.TestFlash.Sync += ( m, c, color ) => flashLastColor = color;

        var cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
        host.SendCommand( TestHelper.Monitor, cmdF, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );
        await cmdF.Completion.Task;

        flashLastColor.ShouldBe( 78 );

        var cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );
        await cmdS.Completion.Task;

        flashLastColor.ShouldBe( 78 );
        cmdF = new FlashCommand() { DeviceName = "n°1", ControllerKey = "Naouak" };
        host.SendCommand( TestHelper.Monitor, cmdF, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );
        await cmdF.Completion.Task;

        flashLastColor.ShouldBe( 3712 );

        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).ShouldBe( DeviceHostCommandResult.CommandCheckValidityFailed );

        cmdS = new SetFlashColorCommand() { DeviceName = "Not the 1", ControllerKey = "Don't care since the device has no controller key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).ShouldBe( DeviceHostCommandResult.DeviceNameNotFound );

        await d.SetControllerKeyAsync( TestHelper.Monitor, null, "The controlling key." );
        cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "Controller key will fail!", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );
        await Util.Awaitable( () => cmdS.Completion.Task ).ShouldThrowAsync<InvalidControllerKeyException>();

        cmdS = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "The controlling key.", Color = 3712 };
        host.SendCommand( TestHelper.Monitor, cmdS, token: cancellation ).ShouldBe( DeviceHostCommandResult.Success );

        await cmdS.Completion.Task;

        await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.ShouldBe( 0 );
        FlashBulb.TotalRunning.ShouldBe( 0 );

    }

    [TestCase( "UseSendCommand" )]
    [TestCase( "UseSendCommandImmediate" )]
    [CancelAfter( 1000 )]
    public async Task sending_commands_checks_DeviceName_and_executing_checks_ControllerKey_Async( string mode, CancellationToken cancellation )
    {
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
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

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

        (await cmdSet.Completion).ShouldBe( 78 );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.ShouldBe( 6 );
        // Completion is signaled and then OnCommandComplededAsyc is called.
        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.ShouldBe( 2 );

        // Use the basic command to set a ControllerKey.
        var setControllerKey = new SetControllerKeyDeviceCommand<FlashBulbHost>()
        {
            ControllerKey = "Never mind since the device's ControllerKey is null.",
            NewControllerKey = "I'm controlling.",
            DeviceName = "n°1"
        };
        SendCommand( setControllerKey );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm not in charge. Completion will throw an InvalidControllerKeyException." };
        SendCommand( cmdSet ).ShouldBeTrue();

        do
        {
            await Task.Delay( 100, cancellation );
        }
        while( !cmdSet.Completion.IsCompleted );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling.", Color = 18 };
        SendCommand( cmdSet );

        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1" };
        SendCommand( cmdRaiseFlash ).ShouldBeTrue();
        await Util.Awaitable( () => cmdRaiseFlash.Completion.Task ).ShouldThrowAsync<InvalidControllerKeyException>();

        flashLastColor.ShouldBe( 6 );
        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.ShouldBe( 5 );

        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm controlling." };
        SendCommand( cmdRaiseFlash );

        await cmdRaiseFlash.Completion.Task;
        flashLastColor.ShouldBe( 18 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.ShouldBe( 6 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling, but checkControllerKey: false is used.", Color = 1 };
        cmdRaiseFlash = new FlashCommand() { DeviceName = "n°1", ControllerKey = "I'm NOT controlling too." };
        SendCommand( cmdSet, checkControllerKey: false );
        SendCommand( cmdRaiseFlash, checkControllerKey: false );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.ShouldBe( 1 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.ShouldBe( 8 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling.", Color = 1 };
        Util.Invokable( () => SendCommand( cmdSet ) ).ShouldThrow<ArgumentException>();

        cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name: this will throw an ArgumentException.", ControllerKey = "I'm controlling." };
        cmdRaiseFlash.DeviceName = "Not the right device name: this will throw an ArgumentException.";
        Util.Invokable( () => SendCommand( cmdRaiseFlash ) ).ShouldThrow<ArgumentException>();

        FlashBulb.OnCommandComplededCount.ShouldBe( 8 );

        cmdSet = new SetFlashColorCommand() { DeviceName = "Not the right device name but checkDeviceName: false is used.", ControllerKey = "I'm controlling.", Color = 3712 };
        cmdRaiseFlash = new FlashCommand() { DeviceName = "Not the right device name too.", ControllerKey = "I'm controlling." };
        SendCommand( cmdSet, checkDeviceName: false );
        SendCommand( cmdRaiseFlash, checkDeviceName: false );
        await cmdRaiseFlash.Completion.Task;
        flashLastColor.ShouldBe( 3712 );

        await Task.Delay( 50, cancellation );
        FlashBulb.OnCommandComplededCount.ShouldBe( 10 );

        await d.DestroyAsync( TestHelper.Monitor );

        FlashBulb.TotalCount.ShouldBe( 0 );
        FlashBulb.TotalRunning.ShouldBe( 0 );

        FlashBulb.OnCommandComplededCount.ShouldBe( 10 );

    }

    [Test]
    public async Task Disabling_sends_a_stop_status_change_Async()
    {
        var host = new MachineHost();

        var config = new MachineConfiguration()
        {
            Name = "Test",
            Status = DeviceConfigurationStatus.AlwaysRunning
        };

        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

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
                    destroyReceived.ShouldBeFalse();
                    destroyReceived = true;
                }
                else if( e.DeviceStatus.HasStopped )
                {
                    // HasStopped is true when IsDestroyed is sent.
                    stopReceived.ShouldBeFalse();
                    stopReceived = true;
                }
            }
        };

        using( TestHelper.Monitor.OpenInfo( "Reconfiguring to Disabled." ).ConcludeWith( () => "Reconfigured to Disabled." ) )
        {
            config.Status = DeviceConfigurationStatus.Disabled;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );
        }
        stopReceived.ShouldBeTrue();
        destroyReceived.ShouldBeFalse();

        await device.DestroyAsync( TestHelper.Monitor );
        destroyReceived.ShouldBeTrue();

        TestHelper.Monitor.Info( "/Disabling_sends_a_stop_status_change_Async" );
    }

}
