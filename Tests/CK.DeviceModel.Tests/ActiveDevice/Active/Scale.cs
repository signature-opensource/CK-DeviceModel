using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{

    public class Scale : ActiveDevice<CommonScaleConfiguration, ScaleEvent>
    {
        PhysicalMachine? _machine;
        int _stepCount;
        int _currentSum;

        public Scale( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
        }

        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            Debug.Assert( _machine == null );
            return Task.CompletedTask;
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, CommonScaleConfiguration config )
        {
            Debug.Assert( IsRunning == (_machine != null) );
            bool physicalRateChanged = config.PhysicalRate != CurrentConfiguration.PhysicalRate;
            if( physicalRateChanged ) return Task.FromResult( DeviceReconfiguredResult.UpdateFailedRestartRequired );
            return Task.FromResult( DeviceReconfiguredResult.UpdateSucceeded );
        }

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            Debug.Assert( _machine == null );
            // We are in the command loop: the CurrentConfiguration cannot change: there
            // is no need to capture a reference to it.
            _machine = new PhysicalMachine( CurrentConfiguration.PhysicalRate, OnPhysicalEvent, CurrentConfiguration.AlwaysPositiveMeasure );
            if( CurrentConfiguration.ResetOnStart ) Reset();
            return Task.FromResult( true );
        }

        void Reset()
        {
            if( _stepCount != 0 || _currentSum != 0 )
            {
                _currentSum = 0;
                _stepCount = 0;
                EventLoop.RaiseEvent( new ScaleResetEvent( this ) );
            }
        }

        void OnPhysicalEvent( int value )
        {
            // We are outside of the command loop here: CurrentConfiguration
            // may change at any time: the right way to use it consistently is
            // to capture a reference to the current one and use it.
            var config = CurrentConfiguration;
            if( value < 0 )
            {
                // This is how to log Error/Warning/Info/Trace/Debug messages
                // outside of the command loop.
                EventLoop.LogWarn( $"Received a negative value (config.StopOnNegativeValue: {config.StopOnNegativeValue})." );
                if( config.StopOnNegativeValue )
                {
                    // Execute has 2 overloads: one with a synchronous Action<IActivityMonitor> and
                    // one for asynchronous Func<IActivityMonitor,Task>.
                    // Here we need to call the asynchronous StopAsync method.
                    EventLoop.LogTrace( "StopOnNegativeValue is true: stopping the device." );
                    EventLoop.Execute( m => StopAsync( m, ignoreAlwaysRunning: true ) );
                    if( config.AllowUnattendedRestartAfterStopOnNegativeValue )
                    {
                        // This is awful and should never be done is real code!
                        // This is just for tests, to avoid subsequent stops.
                        config.StopOnNegativeValue = false;
                        EventLoop.LogTrace( $"Task.Run() in {10 * config.PhysicalRate} ms will restart the device." );
                        _ = Task.Run( async () =>
                        {
                            await Task.Delay( 10 * config.PhysicalRate );
                            EventLoop.Execute( m => StartAsync( m ) );
                        } );
                    }
                }
                return;
            }

            _currentSum += value;
            ++_stepCount;
            EventLoop.LogDebug( $"Measure received: {value}, Sum: {_currentSum}, Step: {_stepCount} (MeasureStep = {CurrentConfiguration.MeasureStep})." );
            if( _stepCount >= CurrentConfiguration.MeasureStep )
            {
                var text = config.MeasurePattern ?? "{0}";
                var m = (double)_currentSum / _stepCount;
                var ev = new ScaleMeasureEvent( this, m, string.Format( text, m ) );
                EventLoop.LogDebug( $"Raised ScaleMeasureEvent: {ev.Measure}." );
                EventLoop.RaiseEvent( ev );
                _stepCount = 0;
            }
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            Debug.Assert( _machine != null );
            _machine.Dispose();
            _machine = null;
            return Task.CompletedTask;
        }

        protected override Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
        {
            switch( command )
            {
                case ScaleResetCommand _:
                    Reset();
                    return Task.CompletedTask;
                default:
                    return base.DoHandleCommandAsync( monitor, command );
            }
        }
    }
}
