using CK.Core;
using CK.Testing;
using CK.Text;
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
                monitor.Trace( $"AlwaysRetryPolicy called. retryCount: {retryCount}." );
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
                int deltaMS = host switch { CameraHost c => CameraDuration * mult, MachineHost m => MachineDuration * mult, _ => OtherDuration * mult };
                monitor.Trace( $"{device.FullName} -> {deltaMS} ms." );
                return deltaMS;
            }
        }

        //[TestCase( "UseAutoDestroy" )]
        [TestCase( "UseHostDestroy" )]
        public async Task simple_auto_restart( string mode )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( "simple_auto_restart" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 200 };
            var host = new MachineHost( policy );

            var daemon = new DeviceHostDaemon( new[] { host } );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host["M"];
            Debug.Assert( d != null );
            d.IsRunning.Should().BeTrue();
            var cmd = new ForceAutoStopCommand<MachineHost>() { DeviceName = "M" };
            d.SendCommand( TestHelper.Monitor, cmd );
            (await cmd.Result.Task).Should().BeTrue();
            d.IsRunning.Should().BeFalse();

            await Task.Delay( 100 );
            d.IsRunning.Should().BeFalse( "Since MinRetryCount = 1." );

            await Task.Delay( 200 );
            d.IsRunning.Should().BeTrue( "Machine started again." );

            if( mode == "UseAutoDestroy" )
            {
                var destroy = new AutoDestroyCommand<MachineHost>() { DeviceName = "M" };
                d.SendCommand( TestHelper.Monitor, destroy );
                await destroy.Result.Task;
            }
            else
            {
                await host.DestroyDeviceAsync( TestHelper.Monitor, "M" );
            }
            d.IsRunning.Should().BeFalse();
            d.IsDestroyed.Should().BeTrue();

            await Task.Delay( 120 );
            d.IsRunning.Should().BeFalse();
            d.IsDestroyed.Should().BeTrue();

            await ((IHostedService)daemon).StopAsync( default );
        }

        [Test]
        public async Task restart_can_be_fast()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( "restart_can_be_fast" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 0, MachineDuration = 200 };
            var host = new MachineHost( policy );
            var daemon = new DeviceHostDaemon( new[] { host } );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d = host["M"];
            Debug.Assert( d != null );
            d.IsRunning.Should().BeTrue();

            var cmd = new ForceAutoStopCommand<MachineHost>() { DeviceName = "M" };
            d.SendCommand( TestHelper.Monitor, cmd );
            (await cmd.Result.Task).Should().BeTrue();
            d.IsRunning.Should().BeFalse();

            await Task.Delay( 20 );

            d.IsRunning.Should().BeTrue();

            await ((IHostedService)daemon).StopAsync( default );
        }

        [Test]
        public async Task multiple_devices_handling()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( "multiple_devices_handling" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1, MachineDuration = 1000 };
            var host = new MachineHost( policy );
            var daemon = new DeviceHostDaemon( new[] { host } );

            await ((IHostedService)daemon).StartAsync( default );
            var config = new MachineConfiguration() { Name = "M", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*2";
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*3";
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            config.Name = "M*4";
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var d1 = host["M"];
            Debug.Assert( d1 != null );
            var d2 = host["M*2"];
            Debug.Assert( d2 != null );
            var d3 = host["M*3"];
            Debug.Assert( d3 != null );
            var d4 = host["M*4"];
            Debug.Assert( d4 != null );

            await d1.SendForceAutoStop( TestHelper.Monitor );
            await d2.SendForceAutoStop( TestHelper.Monitor );
            await d3.SendForceAutoStop( TestHelper.Monitor );
            await d4.SendForceAutoStop( TestHelper.Monitor );

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
        }

        [Test]
        public async Task multiple_hosts_handling()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( "multiple_hosts_handling" );

            var policy = new AlwaysRetryPolicy() { MinRetryCount = 1 };
            var host1 = new MachineHost( policy );
            var host2 = new CameraHost( policy );
            var host3 = new OtherMachineHost( policy );
            var daemon = new DeviceHostDaemon( new IDeviceHost[] { host1, host2, host3 } );

            await ((IHostedService)daemon).StartAsync( default );

            var c1 = new MachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host1.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c1 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c1.Name = "D*2";
            (await host1.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c1 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var c2 = new CameraConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host2.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c2 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c2.Name = "D*2";
            (await host2.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c2 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var c3 = new OtherMachineConfiguration() { Name = "D1", Status = DeviceConfigurationStatus.AlwaysRunning };
            (await host3.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c3 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            c3.Name = "D*2";
            (await host3.ApplyDeviceConfigurationAsync( TestHelper.Monitor, c3 )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            ITestDevice[] devices = ((IDeviceHost)host1).GetConfiguredDevices()
                                    .Concat( ((IDeviceHost)host2).GetConfiguredDevices() )
                                    .Concat( ((IDeviceHost)host3).GetConfiguredDevices() )
                                    .Select( x => (ITestDevice)x.Item1 )
                                    .ToArray();

            foreach( var d in devices )
            {
                await d.SendForceAutoStopAsync( TestHelper.Monitor );
            }
            TestHelper.Monitor.Trace( "*** Wait ***" );
            await Task.Delay( 210 );
            TestHelper.Monitor.Trace( "*** EndWait ***" );
            devices.Count( d => d.IsRunning ).Should().Be( 3 );
            devices.Where( d => d.IsRunning ).Select( d => d.Name ).Concatenate().Should().Be( "D1, D1, D1" );
 
            TestHelper.Monitor.Debug( "*** Wait ***" );
            await Task.Delay( 210 );
            devices.Count( d => d.IsRunning ).Should().Be( 6 );

            // Must destroy the cameras because they are counted!
            await host2.DestroyDeviceAsync( TestHelper.Monitor, "D1" );
            await host2.DestroyDeviceAsync( TestHelper.Monitor, "D*2" );

            await ((IHostedService)daemon).StopAsync( default );
        }
    }
}
