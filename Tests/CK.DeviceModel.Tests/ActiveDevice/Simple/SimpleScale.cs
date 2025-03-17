using CK.Core;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests;

/// <summary>
/// This scale is "simple" only because it's a <see cref="SimpleActiveDevice{TConfiguration, TEvent}"/>
/// rather than an <see cref="ActiveDevice{TConfiguration, TEvent}"/>.
/// <para>
/// There's only the command loop that runs here: the PhysicalEvent (from the machine's timer) is transformed
/// into an internal command that is handled immediately.
/// </para>
/// <para>
/// Events are raised and awaited thanks to the protected <see cref="SimpleActiveDevice{TConfiguration, TEvent}.RaiseEventAsync(IActivityMonitor, TEvent)"/>
/// by the command handlers.
/// </para>
/// </summary>
public class SimpleScale : SimpleActiveDevice<CommonScaleConfiguration, SimpleScaleEvent>
{
    PhysicalMachine? _machine;
    int _stepCount;
    int _currentSum;

    public SimpleScale( IActivityMonitor monitor, CreateInfo info )
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

    protected override async Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
    {
        Debug.Assert( _machine == null );
        // We are in the command loop: the CurrentConfiguration cannot change: there
        // is no need to capture a reference to it.
        _machine = new PhysicalMachine( CurrentConfiguration.PhysicalRate, OnPhysicalEvent, CurrentConfiguration.AlwaysPositiveMeasure );
        if( CurrentConfiguration.ResetOnStart )
        {
            await ResetAsync( monitor );
        }
        return true;
    }

    /// <summary>
    /// The command here has the event as its result.
    /// This is a common pattern (even if here it's useless since this command is not public).
    /// Since there may be no event (on negative value), the event is nullable.
    /// </summary>
    class MeasureCommand : DeviceCommand<SimpleScaleHost, SimpleScaleMeasureEvent?>
    {
        public int Value { get; }

        public MeasureCommand( int v )
        {
            Value = v;
            ImmediateSending = true;
            ShouldCallDeviceOnCommandCompleted = false;
        }
    }

    void OnPhysicalEvent( int value )
    {
        SendRoutedCommandImmediate( new MeasureCommand( value ) );
    }

    Task ResetAsync( IActivityMonitor monitor )
    {
        if( _stepCount != 0 || _currentSum != 0 )
        {
            _currentSum = 0;
            _stepCount = 0;
            return RaiseEventAsync( monitor, new SimpleScaleResetEvent( this ) );
        }
        return Task.CompletedTask;
    }

    async Task HandlePhysicalEventAsync( IActivityMonitor monitor, MeasureCommand cmd )
    {
        // We are in the command loop: the CurrentConfiguration cannot change: there
        // is no need to capture a reference to it.
        if( cmd.Value < 0 )
        {
            monitor.Warn( "Received a negative value." );
            if( CurrentConfiguration.StopOnNegativeValue )
            {
                monitor.Trace( "StopOnNegativeValue is true: stopping the device." );
                // Simply calls StopAsync.
                await StopAsync( monitor, ignoreAlwaysRunning: true );
                if( CurrentConfiguration.AllowUnattendedRestartAfterStopOnNegativeValue )
                {
                    // This is awful and should never be done is real code!
                    // This is just for tests, to avoid subsequent stops.
                    CurrentConfiguration.StopOnNegativeValue = false;
                    // Uses an horrible delayed Task here instead of Reminder: this is just for test!
                    // At least, we correctly capture the CurrentConfiguration here.
                    var config = CurrentConfiguration;
                    monitor.Trace( $"Task.Run() in {10 * config.PhysicalRate} ms will SendRoutedCommandImmediate to restart the device." );
                    _ = Task.Run( async () =>
                    {
                        await Task.Delay( 10 * config.PhysicalRate );
                        SendRoutedCommandImmediate( new StartDeviceCommand<SimpleScaleHost>() );
                    } );
                }
            }
            cmd.Completion.SetResult( null );
            return;
        }
        _currentSum += cmd.Value;
        ++_stepCount;
        monitor.Debug( $"Measure received: {cmd.Value}, Sum: {_currentSum}, Step: {_stepCount} (MeasureStep = {CurrentConfiguration.MeasureStep})." );
        if( _stepCount >= CurrentConfiguration.MeasureStep )
        {
            var text = CurrentConfiguration.MeasurePattern ?? "{0}";
            var m = (double)_currentSum / _stepCount;
            var ev = new SimpleScaleMeasureEvent( this, m, string.Format( text, m ) );
            cmd.Completion.SetResult( ev );
            monitor.Debug( $"Raised SimpleScaleMeasureEvent: {ev.Measure}." );
            await RaiseEventAsync( monitor, ev );
            _stepCount = 0;
        }
        else
        {
            cmd.Completion.SetResult( null );
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
            case MeasureCommand m:
                return HandlePhysicalEventAsync( monitor, m );
            case SimpleScaleResetCommand _:
                return ResetAsync( monitor );
            default:
                return base.DoHandleCommandAsync( monitor, command );
        }
    }
}
