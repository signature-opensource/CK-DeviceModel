//using CK.Core;
//using FluentAssertions;
//using Microsoft.Extensions.Hosting;
//using NUnit.Framework;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using static CK.Testing.MonitorTestHelper;

//namespace CK.DeviceModel.Tests
//{
//    [TestFixture]
//    public class ActiveDeviceTests
//    {
//        [SetUp]
//        public void SetDebugDevice()
//        {
//            ActivityMonitor.Tags.AddFilter( IDeviceHost.DeviceModel, new LogClamper( LogFilter.Debug, false ) );
//        }

//        [TearDown]
//        public void ClearDebugDevice()
//        {
//            ActivityMonitor.Tags.RemoveFilter( IDeviceHost.DeviceModel );
//        }

//        class EventCollector
//        {
//            public readonly List<object> Events = new List<object>();

//            public EventCollector( IActiveDevice device, bool useAllEvent )
//            {
//                if( useAllEvent )
//                {
//                    device.AllEvent.Sync += ( m, e ) => Events.Add( e );
//                }
//                else
//                {
//                    device.LifetimeEvent.Sync += ( m, e ) => LockedAdd( e );
//                    if( device is IActiveDevice<ScaleEvent> scale )
//                    {
//                        scale.DeviceEvent.Sync += ( m, e ) => LockedAdd( e );
//                    }
//                    else if( device is IActiveDevice<SimpleScaleEvent> simple )
//                    {
//                        simple.DeviceEvent.Sync += ( m, e ) => LockedAdd( e );
//                    }
//                }
//            }

//            void LockedAdd( object e )
//            {
//                lock( Events )
//                {
//                    Events.Add( e );
//                }
//            }

//            /// <summary>
//            /// Replaces "SimpleScale" by "Scale" strings make <see cref="SimpleScaleEvent"/> look like <see cref="ScaleEvent"/>.
//            /// </summary>
//            /// <returns></returns>
//            public IEnumerable<string> GetScaleEvents()
//            {
//                return Events.Select( e => e.ToString()!.Replace( "SimpleScale", "Scale" ) );
//            }

//        }


//        [TestCase( "SimpleScale", "RunningReset", "UseAllEvent" )]
//        [TestCase( "SimpleScale", "RunningReset", "UseLifetimeAndDeviceEvent" )]
//        [TestCase( "SimpleScale", "StoppedReset", "UseAllEvent" )]
//        [TestCase( "SimpleScale", "StoppedReset", "UseLifetimeAndDeviceEvent" )]
//        [Timeout(3000)]
//        public async Task collecting_lifetime_reset_and_measure_events_Async( string type, string mode, string useAllEvent )
//        {
//            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( collecting_lifetime_reset_and_measure_events_Async )}(\"{type}\",\"{mode}\",\"{useAllEvent}\")" );
//            IDeviceHost host = type == "SimpleScale"
//                               ? new SimpleScaleHost()
//                               : new ScaleHost();

//            var config = new CommonScaleConfiguration()
//            {
//                Name = "M",
//                MeasurePattern = "Measure!",
//                Status = DeviceConfigurationStatus.Runnable
//            };
//            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

//            var scale = (IActiveDevice?)host.Find( "M" );
//            Debug.Assert( scale != null );
//            scale.IsRunning.Should().BeFalse();

//            var events = new EventCollector( scale, useAllEvent == "UseAllEvent" );

//            // Starts the device.
//            (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();
//            scale.IsRunning.Should().BeTrue();

//            // Let it run to have at least 2 measures.
//            await Task.Delay( 3 * config.PhysicalRate * config.MeasureStep );

//            if( mode == "StoppedReset" ) (await scale.StopAsync( TestHelper.Monitor )).Should().BeTrue();

//            // Sends a reset command.
//            scale.UnsafeSendCommand( TestHelper.Monitor, type == "SimpleScale" ? new SimpleScaleResetCommand() : new ScaleResetCommand() );

//            if( mode == "StoppedReset" ) (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();

//            // Let it run to have at least one measure.
//            await Task.Delay( 2 * config.PhysicalRate * config.MeasureStep );

//            // Stops it.
//            (await scale.StopAsync( TestHelper.Monitor )).Should().BeTrue();
//            scale.IsRunning.Should().BeFalse();

//            if( mode == "StoppedReset" )
//            {
//                events.GetScaleEvents()
//                             .Should().BeEquivalentTo( new[] { "Device 'ScaleHost/M' status changed: Running (StartCall).",
//                                                               "Measure!",
//                                                               "Measure!",
//                                                               "Device 'ScaleHost/M' status changed: Stopped (StoppedCall).",
//                                                               "Reset",
//                                                               "Device 'ScaleHost/M' status changed: Running (StartCall).",
//                                                               "Measure!",
//                                                               "Device 'ScaleHost/M' status changed: Stopped (StoppedCall)." } );
//            }
//            else
//            {
//                events.GetScaleEvents()
//                             .Should().BeEquivalentTo( new[] { "Device 'ScaleHost/M' status changed: Running (StartCall).",
//                                                               "Measure!",
//                                                               "Measure!",
//                                                               "Reset",
//                                                               "Measure!",
//                                                               "Device 'ScaleHost/M' status changed: Stopped (StoppedCall)." } );
//            }

//            var measures = events.Events.OfType<ICommonScaleMeasureEvent>().Select( e => e.Measure ).ToArray();
//            measures[1].Should().BeGreaterThan( measures[0] );
//            measures[1].Should().BeGreaterThan( measures[2] );

//            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
//        }

//        [TestCase( "SimpleScale" )]
//        [TestCase( "Scale" )]
//        [Timeout( 3000 )]
//        public async Task event_loop_can_call_async_device_methods_so_that_a_device_CAN_auto_start_itself_Async( string type )
//        {
//            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( event_loop_can_call_async_device_methods_so_that_a_device_CAN_auto_start_itself_Async )}(\"{type}\")" );
//            IDeviceHost host = type == "SimpleScale"
//                               ? new SimpleScaleHost()
//                               : new ScaleHost();
//            var config = new CommonScaleConfiguration()
//            {
//                Name = "M",
//                StopOnNegativeValue = true,
//                Status = DeviceConfigurationStatus.RunnableStarted
//            };

//            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
//            var scale = host.Find( "M" );
//            Debug.Assert( scale != null );

//            scale.IsRunning.Should().BeTrue();

//            // A negative value has 10% probability to occur, The random seed is fixed.
//            // It appears below 8 measures.
//            await Task.Delay( 8 * config.PhysicalRate );

//            scale.IsRunning.Should().BeFalse( "We obtained a negative value." );

//            config.AllowUnattendedRestartAfterStopOnNegativeValue = true;
//            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

//            // Restarts the device.
//            (await scale.StartAsync( TestHelper.Monitor )).Should().BeTrue();
//            scale.IsRunning.Should().BeTrue();

//            // Wait again for a negative value.
//            await Task.Delay( 8 * config.PhysicalRate );

//            scale.IsRunning.Should().BeFalse( "We obtained a negative value again." );

//            // Wait for the internals to restart.
//            await Task.Delay( 50 * config.PhysicalRate );
//            scale.IsRunning.Should().BeTrue( "The device has been started from the event loop!" );

//            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
//        }

//        [TestCase( "UseAllEvent", "Scale" )]
//        [TestCase( "DeviceEvent", "Scale" )]
//        [TestCase( "UseAllEvent", "SimpleScale" )]
//        [TestCase( "DeviceEvent", "SimpleScale" )]
//        [Timeout( 2000 )]
//        public async Task stress_event_test_Async( string eType, string scaleType )
//        {
//            using var ensureMonitoring = TestHelper.Monitor.OpenInfo( $"{nameof( stress_event_test_Async )}(\"{eType}\",\"{scaleType}\")" );
//            IDeviceHost host = scaleType == "SimpleScale"
//                               ? new SimpleScaleHost()
//                               : new ScaleHost();
//            var config = new CommonScaleConfiguration()
//            {
//                Name = "M",
//                AlwaysPositiveMeasure = true,
//                PhysicalRate = 10,
//                MeasureStep = 1,
//                Status = DeviceConfigurationStatus.Runnable,
//                // We use DebugPostEvent that is sent as an immediate command for SimpleActiveDevice.
//                // This guaranties that all the DebugPostEvent we need to send (100) will be handled
//                // without an intermediate regular command.
//                BaseImmediateCommandLimit = 1000
//            };

//            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );
//            var device = (IActiveDevice?)host.Find( "M" );
//            Debug.Assert( device != null );

//            int debugPostEventCount = 0;
//            int measureEventCount = 0;

//            Scale? scale = device as Scale;
//            SimpleScale? simple = device as SimpleScale;

//            if( scale != null )
//            {
//                if( eType == "AllEvent" )
//                {
//                    scale.AllEvent.Sync += OnScaleDeviceOrAllEvent;
//                }
//                else
//                {
//                    scale.DeviceEvent.Sync += OnScaleDeviceOrAllEvent;
//                }
//            }
//            else if( simple != null )
//            {
//                if( eType == "AllEvent" )
//                {
//                    simple.AllEvent.Sync += OnSimpleScaleDeviceOrAllEvent;
//                }
//                else
//                {
//                    simple.DeviceEvent.Sync += OnSimpleScaleDeviceOrAllEvent;
//                }
//            }
//            else throw new NotSupportedException();

//            void OnScaleDeviceOrAllEvent( IActivityMonitor monitor, BaseDeviceEvent e )
//            {
//                if( e is ScaleMeasureEvent m )
//                {
//                    if( m.ToString() == "DebugPostEvent" )
//                    {
//                        ++debugPostEventCount;
//                    }
//                    else
//                    {
//                        ++measureEventCount;
//                    }
//                }
//                else
//                {
//                    Throw.Exception( "We should only receive ScaleMeasureEvent (some of them being fake DebugPostEvent)." );
//                }
//            }

//            void OnSimpleScaleDeviceOrAllEvent( IActivityMonitor monitor, BaseDeviceEvent e )
//            {
//                if( e is SimpleScaleMeasureEvent m )
//                {
//                    if( m.ToString() == "DebugPostEvent" )
//                    {
//                        ++debugPostEventCount;
//                    }
//                    else
//                    {
//                        ++measureEventCount;
//                    }
//                }
//                else
//                {
//                    Throw.Exception( "We should only receive SimpleScaleMeasureEvent (some of them being fake DebugPostEvent)." );
//                }
//            }

//            #region Test1: Running device: receiving all DebugPostEvent and some MeasureEvent.
//            await device.StartAsync( TestHelper.Monitor );
//            device.IsRunning.Should().BeTrue();

//            for( int i = 0; i < 100; ++i )
//            {
//                BaseActiveDeviceEvent ev = scale != null
//                                            ? new ScaleMeasureEvent( scale, double.MaxValue, "DebugPostEvent" )
//                                            : new SimpleScaleMeasureEvent( simple!, double.MaxValue, "DebugPostEvent" );
//                device.DebugPostEvent( ev );
//            }
//            // Waiting for 1000 ms.
//            // 10ms per event => we should receive 100 measure events (and 100 DebugPostEvent).
//            // In practice, 10 ms for the timer is short. We should receive at least half of them here.
//            await Task.Delay( 1000 );

//            await SendStopAndWaitForSynchronizationAsync( scaleType, device );

//            TestHelper.Monitor.Info( $"Results '{scaleType}': DebugPostEventCount = {debugPostEventCount}, MeasureEventCount = {measureEventCount}." );
//            debugPostEventCount.Should().Be( 100 );
//            measureEventCount.Should().BeGreaterThan( 50 );

//            #endregion

//            #region Test2: Stopped device: no more MeasureEvent. This tests the WaitForSynchronizationAsync.
//            // Sets the command 
//            debugPostEventCount = measureEventCount = 0;
//            for( int i = 0; i < 100; ++i )
//            {
//                BaseActiveDeviceEvent ev = scale != null
//                                            ? new ScaleMeasureEvent( scale, double.MaxValue, "DebugPostEvent" )
//                                            : new SimpleScaleMeasureEvent( simple!, double.MaxValue, "DebugPostEvent" );
//                device.DebugPostEvent( ev );
//            }
//            await SendStopAndWaitForSynchronizationAsync( scaleType, device );
//            debugPostEventCount.Should().Be( 100 );
//            measureEventCount.Should().Be( 0 );
//            #endregion

//            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
//        }

//        private static async Task SendStopAndWaitForSynchronizationAsync( string scaleType, IActiveDevice device )
//        {
//            // Uses SendCommand here instead of calling StopAsync: WaitForSynchronizationAsync will do its job. 
//            BaseStopDeviceCommand stop = scaleType == "SimpleScale"
//                                       ? new StopDeviceCommand<SimpleScaleHost>()
//                                       : new StopDeviceCommand<ScaleHost>();
//            stop.ImmediateSending = false;
//            device.UnsafeSendCommand( TestHelper.Monitor, stop ).Should().BeTrue();

//            (await device.WaitForSynchronizationAsync( true )).Should().Be( WaitForSynchronizationResult.Success );

//            device.IsRunning.Should().BeFalse();
//        }
//    }

//}
