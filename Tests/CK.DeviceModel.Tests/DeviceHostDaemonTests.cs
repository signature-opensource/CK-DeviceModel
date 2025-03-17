using CK.Core;
using Shouldly;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class DeviceHostDaemonTests
{

    public class AlwaysRetryPolicy : IDeviceAlwaysRunningPolicy
    {

        public int CameraDuration { get; set; } = 200;

        public int MachineDuration { get; set; } = 200;

        public int OtherDuration { get; set; } = 200;

        public int MinRetryCount { get; set; } = 1;

        public async Task<int> RetryStartAsync( IActivityMonitor monitor, IDeviceHost host, IDevice device, int retryCount )
        {
            using( monitor.OpenTrace( $"AlwaysRetryPolicy called. retryCount: {retryCount}." ) )
            {
                if( retryCount >= MinRetryCount )
                {
                    monitor.Trace( $"AlwaysRetryPolicy: calling StartAsync." );
                    if( await device.StartAsync( monitor ) )
                    {
                        return 0;
                    }
                }
                var match = Regex.Match( device.Name, ".*\\*(\\d+)" );
                int mult = match.Success ? int.Parse( match.Groups[1].Value ) : 1;
                int deltaMS = host switch { FlashBulbHost _ => CameraDuration * mult, MachineHost _ => MachineDuration * mult, _ => OtherDuration * mult };
                monitor.CloseGroup( $"{device.FullName} -> {deltaMS} ms." );
                return deltaMS;
            }
        }
    }

    [TestCase( "UseDestroyCommandImmediate" )]
    [TestCase( "UseDestroyCommand" )]
    [TestCase( "UseDestroyMethod" )]
    [CancelAfter( 1000 )]
    public async Task simple_auto_restart_Async( string mode, CancellationToken cancellation )
    {
        var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 200 };
        var host = new MachineHost();

        var daemon = new DeviceHostDaemon( new[] { host }, policy );

        await ((IHostedService)daemon).StartAsync( default );
        var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d = host["M"];
        Debug.Assert( d != null );
        d.IsRunning.ShouldBeTrue();

        await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
        d.IsRunning.ShouldBeFalse();

        await Task.Delay( 100, cancellation );
        d.IsRunning.ShouldBeFalse( "Since MinRetryCount = 1." );

        await Task.Delay( 200, cancellation );
        d.IsRunning.ShouldBeTrue( "Machine started again." );

        if( mode == "UseDestroyCommandImmediate" )
        {
            var destroy = new DestroyDeviceCommand<MachineHost>() { DeviceName = "M" };
            d.SendCommand( TestHelper.Monitor, destroy, token: cancellation );
            await destroy.Completion.Task;
        }
        if( mode == "UseDestroyCommand" )
        {
            var destroy = new DestroyDeviceCommand<MachineHost>() { DeviceName = "M", ImmediateSending = false };
            d.SendCommand( TestHelper.Monitor, destroy, token: cancellation );
            await destroy.Completion.Task;
        }
        else
        {
            await d.DestroyAsync( TestHelper.Monitor );
        }
        d.IsRunning.ShouldBeFalse();
        d.IsDestroyed.ShouldBeTrue();

        await Task.Delay( 120, cancellation );
        d.IsRunning.ShouldBeFalse();
        d.IsDestroyed.ShouldBeTrue();

        await ((IHostedService)daemon).StopAsync( default );
    }

    [Test]
    [CancelAfter( 500 )]
    public async Task restart_can_be_fast_Async( CancellationToken cancellation )
    {
        var policy = new AlwaysRetryPolicy() { MinRetryCount = 0, MachineDuration = 200 };
        var host = new MachineHost();
        var daemon = new DeviceHostDaemon( new[] { host }, policy );

        await ((IHostedService)daemon).StartAsync( default );
        var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d = host["M"];
        Debug.Assert( d != null );
        d.IsRunning.ShouldBeTrue();

        (await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true )).ShouldBeTrue();
        // It can be so fast (in release) that the device has already restarted here.
        if( !d.IsRunning )
        {
            await Task.Delay( 20, cancellation );
            d.IsRunning.ShouldBeTrue();
        }

        await ((IHostedService)daemon).StopAsync( default );
    }

    [Test]
    [CancelAfter( 6000 )]
    public async Task multiple_devices_handling_Async( CancellationToken cancellation )
    {
        var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 1000 };
        var host = new MachineHost();
        var daemon = new DeviceHostDaemon( new[] { host }, policy );

        await ((IHostedService)daemon).StartAsync( default );
        var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        config.Name = "M*2";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        config.Name = "M*3";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        config.Name = "M*4";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d1 = host["M"];
        Debug.Assert( d1 != null );
        var d2 = host["M*2"];
        Debug.Assert( d2 != null );
        var d3 = host["M*3"];
        Debug.Assert( d3 != null );
        var d4 = host["M*4"];
        Debug.Assert( d4 != null );

        d1.IsRunning.ShouldBeTrue();
        d2.IsRunning.ShouldBeTrue();
        d3.IsRunning.ShouldBeTrue();
        d4.IsRunning.ShouldBeTrue();

        var stopD1 = new StopDeviceCommand<MachineHost>() { DeviceName = "M", IgnoreAlwaysRunning = true };
        var stopD2NoName = new StopDeviceCommand<MachineHost> { IgnoreAlwaysRunning = true };
        var stopD3 = new StopDeviceCommand<MachineHost>() { DeviceName = "M*3", IgnoreAlwaysRunning = true };
        var stopD4NoName = new StopDeviceCommand<MachineHost> { IgnoreAlwaysRunning = true };

        Util.Invokable( () => d1.SendCommand( TestHelper.Monitor, stopD2NoName ) ).ShouldThrow<ArgumentException>();
        d1.SendCommand( TestHelper.Monitor, stopD1, token: cancellation ).ShouldBeTrue();

        d2.UnsafeSendCommand( TestHelper.Monitor, stopD2NoName, cancellation ).ShouldBeTrue();
        d3.SendCommand( TestHelper.Monitor, stopD3, token: cancellation ).ShouldBeTrue();
        d4.UnsafeSendCommand( TestHelper.Monitor, stopD4NoName, cancellation ).ShouldBeTrue();

        await Task.WhenAll( stopD1.Completion.Task, stopD2NoName.Completion.Task, stopD3.Completion.Task, stopD4NoName.Completion.Task );

        d1.IsRunning.ShouldBeFalse();
        d2.IsRunning.ShouldBeFalse();
        d3.IsRunning.ShouldBeFalse();
        d4.IsRunning.ShouldBeFalse();

        TestHelper.Monitor.Trace( "*** Wait ***" );
        await Task.Delay( 1100, cancellation );
        TestHelper.Monitor.Trace( "*** EndWait ***" );
        d1.IsRunning.ShouldBeTrue();
        d2.IsRunning.ShouldBeFalse();
        d3.IsRunning.ShouldBeFalse();
        d4.IsRunning.ShouldBeFalse();

        TestHelper.Monitor.Debug( "*** Wait ***" );
        await Task.Delay( 1100, cancellation );
        TestHelper.Monitor.Debug( "*** EndWait ***" );
        d1.IsRunning.ShouldBeTrue();
        d2.IsRunning.ShouldBeTrue();
        d3.IsRunning.ShouldBeFalse();
        d4.IsRunning.ShouldBeFalse();

        TestHelper.Monitor.Debug( "*** Wait ***" );
        await Task.Delay( 1100, cancellation );
        TestHelper.Monitor.Debug( "*** EndWait ***" );
        d1.IsRunning.ShouldBeTrue();
        d2.IsRunning.ShouldBeTrue();
        d3.IsRunning.ShouldBeTrue();
        d4.IsRunning.ShouldBeFalse();

        TestHelper.Monitor.Debug( "*** Wait ***" );
        await Task.Delay( 1100, cancellation );
        TestHelper.Monitor.Debug( "*** EndWait ***" );
        d1.IsRunning.ShouldBeTrue();
        d2.IsRunning.ShouldBeTrue();
        d3.IsRunning.ShouldBeTrue();
        d4.IsRunning.ShouldBeTrue();

        await ((IHostedService)daemon).StopAsync( default );
        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
    }

    [TestCase( OnStoppedDaemonBehavior.ClearAllHosts )]
    [TestCase( OnStoppedDaemonBehavior.ClearAllHostsAndWaitForDevicesDestroyed )]
    [CancelAfter( 2000 )]
    public async Task multiple_hosts_handling_and_OnStoppedDaemonBehavior_Async( OnStoppedDaemonBehavior behavior, CancellationToken cancellation )
    {
        var policy = new AlwaysRetryPolicy() { MinRetryCount = 1 };
        var host1 = new MachineHost();
        var host2 = new FlashBulbHost();
        var host3 = new OtherMachineHost();
        var daemon = new DeviceHostDaemon( new IDeviceHost[] { host1, host2, host3 }, policy );
        daemon.StoppedBehavior = behavior;

        await ((IHostedService)daemon).StartAsync( default );

        var c1 = new MachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host1.EnsureDeviceAsync( TestHelper.Monitor, c1 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        c1.Name = "D*2";
        (await host1.EnsureDeviceAsync( TestHelper.Monitor, c1 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var c2 = new FlashBulbConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host2.EnsureDeviceAsync( TestHelper.Monitor, c2 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        c2.Name = "D*2";
        (await host2.EnsureDeviceAsync( TestHelper.Monitor, c2 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var c3 = new OtherMachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host3.EnsureDeviceAsync( TestHelper.Monitor, c3 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        c3.Name = "D*2";
        (await host3.EnsureDeviceAsync( TestHelper.Monitor, c3 )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        IDevice[] devices = ((IDeviceHost)host1).GetDevices().Values
                                .Concat( ((IDeviceHost)host2).GetDevices().Values )
                                .Concat( ((IDeviceHost)host3).GetDevices().Values )
                                .ToArray();

        foreach( var d in devices )
        {
            await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
        }
        TestHelper.Monitor.Trace( "*** Wait ***" );
        await Task.Delay( 300, cancellation );
        TestHelper.Monitor.Trace( "*** EndWait ***" );
        devices.Count( d => d.IsRunning ).ShouldBe( 3 );
        devices.Where( d => d.IsRunning ).Select( d => d.Name ).Concatenate().ShouldBe( "D1, D1, D1" );

        TestHelper.Monitor.Debug( "*** Wait ***" );
        await Task.Delay( 300, cancellation );
        devices.Count( d => d.IsRunning ).ShouldBe( 6 );

        await ((IHostedService)daemon).StopAsync( default );
        if( behavior == OnStoppedDaemonBehavior.ClearAllHosts )
        {
            TestHelper.Monitor.Trace( "*** Wait ***" );
            await Task.Delay( 300, cancellation );
        }
        host1.Count.ShouldBe( 0 );
        host2.Count.ShouldBe( 0 );
        host3.Count.ShouldBe( 0 );
    }

    [Test]
    [CancelAfter( 6000 )]
    public async Task DefaultDeviceAlwaysRunningPolicy_always_retry_by_default_Async( CancellationToken cancellation )
    {
        var policy = new DefaultDeviceAlwaysRunningPolicy();
        var host = new MachineHost();

        var daemon = new DeviceHostDaemon( [host], policy );

        await ((IHostedService)daemon).StartAsync( default );
        var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var d = host["M"];
        Debug.Assert( d != null );
        d.IsRunning.ShouldBeTrue();

        TestHelper.Monitor.Info( "The device now fails to start. Force stop it (ignoreAlwaysRunning: true) and reset the counter." );
        Machine.TotalRunning = 0;
        d.FailToStart = true;
        await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
        d.IsRunning.ShouldBeFalse();
        TestHelper.Monitor.Info( $"The device should now be restarted by the daemon in {policy.RetryTimeouts.Select( t => t.ToString() ).Concatenate()} ms." );

        // Waiting for all the timeouts.
        foreach( var delay in policy.RetryTimeouts )
        {
            TestHelper.Monitor.Info( $"Waiting for {delay} ms" );
            await Task.Delay( delay, cancellation );
            d.IsRunning.ShouldBeFalse();
        }
        // Waiting for 2 more attempts.
        for( int i = 0; i < 2; ++i )
        {
            TestHelper.Monitor.Info( $"To be sure that we have honored the 'alwaysRetry' parameter. Waiting for last (repeated) timeout of the policy ({policy.RetryTimeouts[^1]} ms)." );
            await Task.Delay( policy.RetryTimeouts[^1], cancellation );
            d.IsRunning.ShouldBeFalse();
        }
        TestHelper.Monitor.Info( $"Daemon has called Start {Machine.TotalRunning} times. This should be greater than {policy.RetryTimeouts.Count}." );
        Machine.TotalRunning.ShouldBeGreaterThan( policy.RetryTimeouts.Count );

        TestHelper.Monitor.Info( $"Let the device be started again and wait for the last timeout ({policy.RetryTimeouts[^1]} + 50 ms). The device must be started." );
        d.FailToStart = false;
        await Task.Delay( policy.RetryTimeouts[^1] + 50, cancellation );
        d.IsRunning.ShouldBeTrue();

        TestHelper.Monitor.Info( $"Destroy the device and stop the daemon." );
        await d.DestroyAsync( TestHelper.Monitor );
        d.IsRunning.ShouldBeFalse();
        d.IsDestroyed.ShouldBeTrue();
        await ((IHostedService)daemon).StopAsync( default );
    }

}
