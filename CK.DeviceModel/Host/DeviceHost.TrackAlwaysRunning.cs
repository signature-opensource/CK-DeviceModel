using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        ( IDevice Device, int Count, DateTime NextCall)[] _alwayRunningStoppedSafe;

        void IInternalDeviceHost.SetDaemon( DeviceHostDaemon daemon )
        {
            Debug.Assert( daemon != null );
            _daemon = daemon;
        }

        void IInternalDeviceHost.OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor )
        {
            Debug.Assert( _lock.IsEnteredBy( monitor ) );
            int idx = _alwayRunningStopped.IndexOf( e => e.Device == d );
            if( idx >= 0 )
            {
                if( d.IsRunning || d.ConfigurationStatus != DeviceConfigurationStatus.AlwaysRunning )
                {
                    _alwayRunningStopped.RemoveAt( idx );
                    _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
                }
                else
                {
                    Debug.Assert( d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning );
                    // Let the NextCall unchanged: manual Starts are ignored.
                    _alwayRunningStopped[idx] = (d, _alwayRunningStopped[idx].Count + 1, _alwayRunningStopped[idx].NextCall );
                    _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
                    _daemon?.Signal();
                }
            }
            else
            {
                if( !d.IsRunning && d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning )
                {
                    // Next call (Count = 0) is asap.
                    _alwayRunningStopped.Add( (d, 0, Util.UtcMinValue) );
                    _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
                    _daemon?.Signal();
                }
            }
        }

        async ValueTask<int> IInternalDeviceHost.CheckAlwaysRunningAsync( IActivityMonitor monitor, DateTime now )
        {
            // Fast path: nothing to do (hence the ValueTask).
            var devices = _alwayRunningStoppedSafe;
            if( devices.Length == 0 ) return Int32.MaxValue;

            // Slow path: check times and call Policy.RetryStartAsync.
            int[] updatedDeltas = new int[devices.Length];
            try
            {
                int idx = 0;
                foreach( var e in devices )
                {
                    var d = e.Device;
                    // Delta 0 means: the device should be removed from the _alwayRunningStopped list.
                    int delta = 0;
                    if( !d.IsRunning && d.ConfigurationStatus == DeviceConfigurationStatus.AlwaysRunning )
                    {
                        delta = (int)((now - e.NextCall).Ticks / TimeSpan.TicksPerMillisecond);
                        if( delta <= 0 )
                        {
                            delta = await _alwaysRunningPolicy.RetryStartAsync( monitor, this, d, e.Count );
                            if( d.IsRunning || delta < 0 ) delta = 0;
                            // A positive Delta means that the NextCallDate in the _alwayRunningStopped list must be updated.
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
            // Very slow path: update the list.
            bool changed = false;
            int minDelta = Int32.MaxValue;
            using( _lock.EnterAsync( monitor ) )
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
                            _alwayRunningStopped[idx] = (d, _alwayRunningStopped[idx].Count, now.AddMilliseconds( delta ));
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
                    _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
                }
            }
            return minDelta;
        }
    }


}
