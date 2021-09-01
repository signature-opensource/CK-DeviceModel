using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
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

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class ActiveDeviceTests
    {
        class EventCollector
        {
            public readonly List<object> Events = new List<object>();

            public EventCollector( SimpleScale device, bool useAllEvent )
            {
                if( useAllEvent )
                {
                    device.AllEvent.Sync += ( m, e ) => Events.Add( e );
                }
                else
                {
                    device.LifetimeEvent.Sync += ( m, e ) => LockedAdd( e );
                    device.DeviceEvent.Sync += ( m, e ) => LockedAdd( e );
                }
            }

            void LockedAdd( object e )
            {
                lock( Events )
                {
                    Events.Add( e );
                }
            }

        }


        [TestCase( "RunningReset", "UseAllEvent" )]
        [TestCase( "RunningReset", "UseLifetimeAndDeviceEvent" )]
        [TestCase( "StoppedReset", "UseAllEvent" )]
        [TestCase( "StoppedReset", "UseLifetimeAndDeviceEvent" )]
        public async Task collecting_lifetime_reset_and_measure_events_shows_that_Reset_command_has_a_RunAnyway_stopped_behavior( string mode, string useAllEvent )
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( collecting_lifetime_reset_and_measure_events_shows_that_Reset_command_has_a_RunAnyway_stopped_behavior ) );
            var host = new SimpleScaleHost();

            var config = new SimpleScaleConfiguration()
            {
                Name = "M",
                MeasurePattern = "Measure!",
                Status = DeviceConfigurationStatus.Runnable
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            var scale = host.Find( "M" );
            Debug.Assert( scale != null );
            scale.IsRunning.Should().BeFalse();

            var events = new EventCollector( scale, useAllEvent == "UseAllEvent" );

            // Starts the device.
            (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            scale.IsRunning.Should().BeTrue();

            // Let it run to have at least 2 measures.
            await Task.Delay( 3 * config.PhysicalRate * config.MeasureStep );

            if( mode == "StoppedReset" ) (await scale.StopAsync( TestHelper.Monitor )).Should().BeTrue();

            // Sends a reset command.
            scale.UnsafeSendCommand( TestHelper.Monitor, new SimpleScaleResetCommand() );

            if( mode == "StoppedReset" ) (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();

            // Let it run to have at least one measure.
            await Task.Delay( 2 * config.PhysicalRate * config.MeasureStep );

            // Stops it.
            (await scale.StopAsync( TestHelper.Monitor )).Should().BeTrue();
            scale.IsRunning.Should().BeFalse();

            if( mode == "StoppedReset" )
            {
                events.Events.Select( e => e.ToString() )
                             .Should().BeEquivalentTo( "Device 'SimpleScaleHost/M' status changed: Running (StartCall).",
                                                       "Measure!",
                                                       "Measure!",
                                                       "Device 'SimpleScaleHost/M' status changed: Stopped (StoppedCall).",
                                                       "Reset",
                                                       "Device 'SimpleScaleHost/M' status changed: Running (StartCall).",
                                                       "Measure!",
                                                       "Device 'SimpleScaleHost/M' status changed: Stopped (StoppedCall)." );
            }
            else
            {
                events.Events.Select( e => e.ToString() )
                             .Should().BeEquivalentTo( "Device 'SimpleScaleHost/M' status changed: Running (StartCall).",
                                                       "Measure!",
                                                       "Measure!",
                                                       "Reset",
                                                       "Measure!",
                                                       "Device 'SimpleScaleHost/M' status changed: Stopped (StoppedCall)." );
            }

            var measures = events.Events.OfType<SimpleScaleMeasureEvent>().Select( e => e.Measure ).ToArray();
            measures[1].Should().BeGreaterThan( measures[0] );
            measures[1].Should().BeGreaterThan( measures[2] );

            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        }

        [Test]
        public async Task event_loop_can_call_async_device_methods_so_that_a_device_CAN_auto_start_itself()
        {
            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( nameof( event_loop_can_call_async_device_methods_so_that_a_device_CAN_auto_start_itself ) );
            var host = new SimpleScaleHost();
            var config = new SimpleScaleConfiguration()
            {
                Name = "M",
                StopOnNegativeValue = true,
                Status = DeviceConfigurationStatus.RunnableStarted
            };

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var scale = host.Find( "M" );
            Debug.Assert( scale != null );

            scale.IsRunning.Should().BeTrue();

            // A negative value has 10% probability to occur, The random seed is fixed.
            // It appears below 8 measures.
            await Task.Delay( 8 * config.PhysicalRate );

            scale.IsRunning.Should().BeFalse( "We obtained a negative value." );

            config.AllowUnattendedRestartAfterStopOnNegativeValue = true;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            // Restarts the device.
            (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();
            scale.IsRunning.Should().BeTrue();

            // Wait again for a negative value.
            await Task.Delay( 8 * config.PhysicalRate );
            scale.IsRunning.Should().BeFalse( "We obtained a negative value again." );

            // Wait for the internals to restart.
            await Task.Delay( 50 * config.PhysicalRate );
            scale.IsRunning.Should().BeTrue( "The device has been started from the event loop!" );

            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        }

    }

}
