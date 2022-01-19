using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
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
        [Timeout( 1000 )]
        public async Task simple_auto_restart_Async( string mode )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof(simple_auto_restart_Async)}(\"{mode}\")" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 200 };
            var host = new MachineHost();

            var daemon = new DeviceHostDaemon( new[] { host }, policy );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host["M"];
            Debug.Assert( d != null );
            d.IsRunning.Should().BeTrue();

            await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
            d.IsRunning.Should().BeFalse();

            await Task.Delay( 100 );
            d.IsRunning.Should().BeFalse( "Since MinRetryCount = 1." );

            await Task.Delay( 200 );
            d.IsRunning.Should().BeTrue( "Machine started again." );

            if( mode == "UseDestroyCommandImmediate" )
            {
                var destroy = new DestroyDeviceCommand<MachineHost>() { DeviceName = "M" };
                d.SendCommand( TestHelper.Monitor, destroy );
                await destroy.Completion.Task;
            }
            if( mode == "UseDestroyCommand" )
            {
                var destroy = new DestroyDeviceCommand<MachineHost>() { DeviceName = "M", ImmediateSending = false };
                d.SendCommand( TestHelper.Monitor, destroy );
                await destroy.Completion.Task;
            }
            else
            {
                await d.DestroyAsync( TestHelper.Monitor );
            }
            d.IsRunning.Should().BeFalse();
            d.IsDestroyed.Should().BeTrue();

            await Task.Delay( 120 );
            d.IsRunning.Should().BeFalse();
            d.IsDestroyed.Should().BeTrue();

            await ((IHostedService)daemon).StopAsync( default );
        }

        [Test]
        [Timeout( 500 )]
        public async Task restart_can_be_fast_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof(restart_can_be_fast_Async) );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 0, MachineDuration = 200 };
            var host = new MachineHost();
            var daemon = new DeviceHostDaemon( new[] { host }, policy );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host["M"];
            Debug.Assert( d != null );
            d.IsRunning.Should().BeTrue();

            (await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true )).Should().BeTrue();
            // It can be so fast (in release) that the device has already restarted here.
            if( !d.IsRunning )
            {
                await Task.Delay( 20 );
                d.IsRunning.Should().BeTrue();
            }

            await ((IHostedService)daemon).StopAsync( default );
        }

        [Test]
        [Timeout( 6000 )]
        public async Task multiple_devices_handling_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof(multiple_devices_handling_Async) );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 1000 };
            var host = new MachineHost();
            var daemon = new DeviceHostDaemon( new[] { host }, policy );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*2";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*3";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*4";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d1 = host["M"];
            Debug.Assert( d1 != null );
            var d2 = host["M*2"];
            Debug.Assert( d2 != null );
            var d3 = host["M*3"];
            Debug.Assert( d3 != null );
            var d4 = host["M*4"];
            Debug.Assert( d4 != null );

            d1.IsRunning.Should().BeTrue();
            d2.IsRunning.Should().BeTrue();
            d3.IsRunning.Should().BeTrue();
            d4.IsRunning.Should().BeTrue();

            var stopD1 = new StopDeviceCommand<MachineHost>() { DeviceName = "M", IgnoreAlwaysRunning = true };
            var stopD2NoName = new StopDeviceCommand<MachineHost> { IgnoreAlwaysRunning = true };
            var stopD3 = new StopDeviceCommand<MachineHost>() { DeviceName = "M*3", IgnoreAlwaysRunning = true };
            var stopD4NoName = new StopDeviceCommand<MachineHost> { IgnoreAlwaysRunning = true };

            d1.Invoking( _ => _.SendCommand( TestHelper.Monitor, stopD2NoName ) ).Should().Throw<ArgumentException>();
            d1.SendCommand( TestHelper.Monitor, stopD1 ).Should().BeTrue();

            d2.UnsafeSendCommand( TestHelper.Monitor, stopD2NoName ).Should().BeTrue();
            d3.SendCommand( TestHelper.Monitor, stopD3 ).Should().BeTrue();
            d4.UnsafeSendCommand( TestHelper.Monitor, stopD4NoName ).Should().BeTrue();

            await Task.WhenAll( stopD1.Completion.Task, stopD2NoName.Completion.Task, stopD3.Completion.Task, stopD4NoName.Completion.Task );

            d1.IsRunning.Should().BeFalse();
            d2.IsRunning.Should().BeFalse();
            d3.IsRunning.Should().BeFalse();
            d4.IsRunning.Should().BeFalse();

            TestHelper.Monitor.Trace( "*** Wait ***" );
            await Task.Delay( 1100 );
            TestHelper.Monitor.Trace( "*** EndWait ***" );
            d1.IsRunning.Should().BeTrue();
            d2.IsRunning.Should().BeFalse();
            d3.IsRunning.Should().BeFalse();
            d4.IsRunning.Should().BeFalse();

            TestHelper.Monitor.Debug( "*** Wait ***" );
            await Task.Delay( 1100 );
            TestHelper.Monitor.Debug( "*** EndWait ***" );
            d1.IsRunning.Should().BeTrue();
            d2.IsRunning.Should().BeTrue();
            d3.IsRunning.Should().BeFalse();
            d4.IsRunning.Should().BeFalse();

            TestHelper.Monitor.Debug( "*** Wait ***" );
            await Task.Delay( 1100 );
            TestHelper.Monitor.Debug( "*** EndWait ***" );
            d1.IsRunning.Should().BeTrue();
            d2.IsRunning.Should().BeTrue();
            d3.IsRunning.Should().BeTrue();
            d4.IsRunning.Should().BeFalse();

            TestHelper.Monitor.Debug( "*** Wait ***" );
            await Task.Delay( 1100 );
            TestHelper.Monitor.Debug( "*** EndWait ***" );
            d1.IsRunning.Should().BeTrue();
            d2.IsRunning.Should().BeTrue();
            d3.IsRunning.Should().BeTrue();
            d4.IsRunning.Should().BeTrue();

            await ((IHostedService)daemon).StopAsync( default );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        }

        [TestCase( OnStoppedDaemonBehavior.ClearAllHosts )]
        [TestCase( OnStoppedDaemonBehavior.ClearAllHostsAndWaitForDevicesDestroyed )]
        [Timeout( 2000 )]
        public async Task multiple_hosts_handling_and_OnStoppedDaemonBehavior_Async( OnStoppedDaemonBehavior behavior )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( multiple_hosts_handling_and_OnStoppedDaemonBehavior_Async )}({behavior})" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1 };
            var host1 = new MachineHost();
            var host2 = new FlashBulbHost();
            var host3 = new OtherMachineHost();
            var daemon = new DeviceHostDaemon( new IDeviceHost[] { host1, host2, host3 }, policy );
            daemon.StoppedBehavior = behavior;

            await ((IHostedService)daemon).StartAsync( default );

            var c1 = new MachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host1.EnsureDeviceAsync( TestHelper.Monitor, c1 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c1.Name = "D*2";
            (await host1.EnsureDeviceAsync( TestHelper.Monitor, c1 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var c2 = new FlashBulbConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host2.EnsureDeviceAsync( TestHelper.Monitor, c2 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c2.Name = "D*2";
            (await host2.EnsureDeviceAsync( TestHelper.Monitor, c2 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var c3 = new OtherMachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host3.EnsureDeviceAsync( TestHelper.Monitor, c3 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c3.Name = "D*2";
            (await host3.EnsureDeviceAsync( TestHelper.Monitor, c3 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            IDevice[] devices = ((IDeviceHost)host1).GetDevices().Values
                                    .Concat( ((IDeviceHost)host2).GetDevices().Values )
                                    .Concat( ((IDeviceHost)host3).GetDevices().Values )
                                    .ToArray();

            foreach( var d in devices )
            {
                await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
            }
            TestHelper.Monitor.Trace( "*** Wait ***" );
            await Task.Delay( 300 );
            TestHelper.Monitor.Trace( "*** EndWait ***" );
            devices.Count( d => d.IsRunning ).Should().Be( 3 );
            devices.Where( d => d.IsRunning ).Select( d => d.Name ).Concatenate().Should().Be( "D1, D1, D1" );
 
            TestHelper.Monitor.Debug( "*** Wait ***" );
            await Task.Delay( 300 );
            devices.Count( d => d.IsRunning ).Should().Be( 6 );

            await ((IHostedService)daemon).StopAsync( default );
            if( behavior == OnStoppedDaemonBehavior.ClearAllHosts )
            {
                TestHelper.Monitor.Trace( "*** Wait ***" );
                await Task.Delay( 300 );
            }
            host1.Count.Should().Be( 0 );
            host2.Count.Should().Be( 0 );
            host3.Count.Should().Be( 0 );
        }

        [Test]
        [Timeout( 6000 )]
        public async Task DefaultDeviceAlwaysRunningPolicy_always_retry_by_default_Async()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( DefaultDeviceAlwaysRunningPolicy_always_retry_by_default_Async ) );

            var policy = new DefaultDeviceAlwaysRunningPolicy();
            var host = new MachineHost();

            var daemon = new DeviceHostDaemon( new[] { host }, policy );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host["M"];
            Debug.Assert( d != null );
            d.IsRunning.Should().BeTrue();

            TestHelper.Monitor.Info( "The device now fails to start. Force stop it (ignoreAlwaysRunning: true) and reset the counter." );
            Machine.TotalRunning = 0;
            d.FailToStart = true;
            await d.StopAsync( TestHelper.Monitor, ignoreAlwaysRunning: true );
            d.IsRunning.Should().BeFalse();
            TestHelper.Monitor.Info( $"The device should now be restarted by the daemon in {policy.RetryTimeouts.Select( t => t.ToString() ).Concatenate()} ms." );

            // Waiting for all the timeouts.
            foreach( var delay in policy.RetryTimeouts )
            {
                TestHelper.Monitor.Info( $"Waiting for {delay} ms" );
                await Task.Delay( delay );
                d.IsRunning.Should().BeFalse();
            }
            // Waiting for 2 more attempts.
            for( int i = 0; i < 2; ++i )
            {
                TestHelper.Monitor.Info( $"To be sure that we have honored the 'alwaysRetry' parameter. Waiting for last (repeated) timeout of the policy ({policy.RetryTimeouts[^1]} ms)." );
                await Task.Delay( policy.RetryTimeouts[^1] );
                d.IsRunning.Should().BeFalse();
            }
            TestHelper.Monitor.Info( $"Daemon has called Start {Machine.TotalRunning} times. This should be greater than {policy.RetryTimeouts.Count}." );
            Machine.TotalRunning.Should().BeGreaterThan( policy.RetryTimeouts.Count );

            TestHelper.Monitor.Info( $"Let the device be started again and wait for the last timeout ({policy.RetryTimeouts[^1]} + 50 ms). The device must be started." );
            d.FailToStart = false;
            await Task.Delay( policy.RetryTimeouts[^1] + 50 );
            d.IsRunning.Should().BeTrue();

            TestHelper.Monitor.Info( $"Destroy the device and stop the daemon." );
            await d.DestroyAsync( TestHelper.Monitor );
            d.IsRunning.Should().BeFalse();
            d.IsDestroyed.Should().BeTrue();
            await ((IHostedService)daemon).StopAsync( default );
        }

    }
}
