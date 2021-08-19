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

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class ActiveDeviceTests
    {
        class EventCollector
        {
            public readonly List<object> Events = new List<object>();

            public EventCollector( SimpleScale device )
            {
                device.LifetimeEvent.Sync += (m, e) => Add( e );
                device.DeviceEvent.Sync += (m, d, e) => Add( e );
            }

            void Add( object e )
            {
                lock( Events )
                {
                    Events.Add( e );
                }
            }

        }


        [TestCase( "StoppedReset" )]
        [TestCase( "RunningReset" )]
        public async Task collecting_lifetime_reset_and_measure_events_shows_that_Reset_command_has_a_RunAnyway_stopped_behavior( string mode )
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

            var events = new EventCollector( scale );

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
        }

    }
}
