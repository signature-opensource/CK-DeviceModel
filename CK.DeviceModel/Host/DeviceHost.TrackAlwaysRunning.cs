using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace CK.DeviceModel
{
    public abstract partial class DeviceHost<T, THostConfiguration, TConfiguration> where T : Device<TConfiguration>
        where THostConfiguration : DeviceHostConfiguration<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        readonly List<(IDevice Device, int Count, DateTime NextCall)> _alwayRunningStopped;
        readonly IDeviceAlwaysRunningPolicy _alwaysRunningPolicy;
        DeviceHostDaemon? _daemon;
        volatile (IDevice Device, int Count, DateTime NextCall)[] _alwayRunningStoppedSafe;

        void IInternalDeviceHost.SetDaemon( DeviceHostDaemon daemon )
        {
            Debug.Assert( daemon != null );
            _daemon = daemon;
        }

        void IInternalDeviceHost.OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor )
        {
            // Here _lock is entered by an external monitor, or by the device's _commandMonitor.
            using( monitor.OpenDebug( $"OnAlwaysRunningCheck for device '{d}'." ) )
            {
                int idx = _alwayRunningStopped.IndexOf( e => e.Device == d );
                if( idx >= 0 )
                {
                    monitor.Debug( "Device is registered." );
                    if( d.IsRunning || d.ConfigurationStatus != DeviceConfigurationStatus.AlwaysRunning )
                    {
                        _alwayRunningStopped.RemoveAt( idx );
                        CaptureAlwayRunningStoppedSafe( monitor, false );
                    }
                    else
                    {
                        Debug.Assert( d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning );
                        // Let the NextCall unchanged: manual Starts are ignored.
                        _alwayRunningStopped[idx] = (d, _alwayRunningStopped[idx].Count + 1, _alwayRunningStopped[idx].NextCall);
                        CaptureAlwayRunningStoppedSafe( monitor, true );
                    }
                }
                else
                {
                    if( !d.IsRunning && d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning )
                    {
                        // Next call (Count = 0) is asap.
                        _alwayRunningStopped.Add( (d, 0, DateTime.UtcNow) );
                        CaptureAlwayRunningStoppedSafe( monitor, true );
                    }
                    else
                    {
                        monitor.CloseGroup( "Nothing to do (device is running and was not registered)." );
                    }
                }
            }
        }

        async ValueTask<long> IInternalDeviceHost.CheckAlwaysRunningAsync( IActivityMonitor monitor, DateTime now )
        {
            // Fast path: nothing to do (hence the ValueTask).
            var devices = _alwayRunningStoppedSafe;
            if( devices.Length == 0 ) return Int64.MaxValue;

            // Slow path: check times and call Policy.RetryStartAsync.
            long[] updatedDeltas = new long[devices.Length];
            try
            {
                int idx = 0;
                foreach( var e in devices )
                {
                    var d = e.Device;
                    // Delta 0 means: the device should be removed from the _alwayRunningStopped list.
                    long delta = 0;
                    if( !d.IsRunning && d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning )
                    {
                        delta = (e.NextCall - now).Ticks;
                        if( delta <= 0 )
                        {
                            delta = await _alwaysRunningPolicy.RetryStartAsync( monitor, this, d, e.Count ).ConfigureAwait( false );
                            if( d.IsRunning || delta < 0 ) delta = 0;
                            else
                            {
                                delta *= TimeSpan.TicksPerMillisecond;
                                // A positive Delta means that the NextCallDate in the _alwayRunningStopped list must be updated.
                            }
                        }
                        else
                        {
                            // When Delta is negative, it means that the device must keep its NextCallDate in the _alwayRunningStopped list.
                            delta = -delta;
                        }
                    }
                    updatedDeltas[idx++] = delta;
                }
            }
            catch( Exception ex )
            {
                monitor.Fatal( $"Buggy retry policy {_alwaysRunningPolicy}.", ex );
                return Int32.MaxValue;
            }
            // Lock the host: update the list.
            bool changed = false;
            long minDelta = Int64.MaxValue;
            using( await _lock.LockAsync( monitor ) )
            {
                for( int i = 0; i < devices.Length; ++i )
                {
                    var d = devices[i].Device;
                    int idx = _alwayRunningStopped.IndexOf( e => e.Device == d );
                    if( idx >= 0 )
                    {
                        var delta = updatedDeltas[i];
                        if( d.IsRunning || d.ConfigurationStatus != DeviceConfigurationStatus.AlwaysRunning ) delta = 0;
                        if( delta > 0 )
                        {
                            changed = true;
                            _alwayRunningStopped[idx] = (d, _alwayRunningStopped[idx].Count + 1, now.AddTicks( delta ));
                            if( minDelta > delta ) minDelta = delta;
                        }
                        else if( delta == 0 )
                        {
                            changed = true;
                            _alwayRunningStopped.RemoveAt( idx );
                        }
                        else
                        {
                            // Not touched.
                            delta = -delta;
                            Debug.Assert( delta > 0 );
                            if( minDelta > delta ) minDelta = delta;
                        }
                    }
                }
                if( changed )
                {
                    CaptureAlwayRunningStoppedSafe( monitor, false );
                }
            }
            return minDelta;
        }

        private void CaptureAlwayRunningStoppedSafe( IActivityMonitor monitor, bool signalHost )
        {
            _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
            if( monitor.ShouldLogLine( LogLevel.Debug ) )
            {
                monitor.UnfilteredLog( null, LogLevel.Debug|LogLevel.IsFiltered, $"Updated Always Running Stopped list of '{DeviceHostName}': ({_alwayRunningStoppedSafe.Select( e => $"{e.Device.Name}, {e.Count}, { e.NextCall.ToString( "HH:mm.ss.ff" )})" ).Concatenate( ", (" )}).", monitor.NextLogTime(), null );
                monitor.UnfilteredLog( null, LogLevel.Debug|LogLevel.IsFiltered, signalHost ? "Host signaled!" : "(no signal.)", monitor.NextLogTime(), null );
            }
            if( signalHost ) _daemon?.Signal();
        }
    }


}
