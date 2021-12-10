using CK.Core;
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
        /// <summary>
        /// This contains the devices for which a call to the <see cref="IDeviceAlwaysRunningPolicy.RetryStartAsync(IActivityMonitor, IDeviceHost, IDevice, int)"/>
        /// is planned. We lock it when reading or modifying it (instead of creating an extra lock object). 
        /// </summary>
        readonly List<(IDevice Device, int Count, DateTime NextCall)> _alwayRunningStopped;
        DeviceHostDaemon? _daemon;

        /// <summary>
        /// This is a snapshot of the _alwayRunningStopped list.
        /// The daemon uses this without lock.
        /// </summary>
        volatile (IDevice Device, int Count, DateTime NextCall)[] _alwayRunningStoppedSafe;

        /// <summary>
        /// Extension point that enables this host to handle its own <see cref="DeviceConfigurationStatus.AlwaysRunning"/> retry policy.
        /// <para>
        /// This default implementation is a simple relay to the <paramref name="global"/> <see cref="IDeviceAlwaysRunningPolicy.RetryStartAsync"/>
        /// method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="global">The globally available policy.</param>
        /// <param name="device">The faulty device.</param>
        /// <param name="retryCount">
        /// The number of previous attempts to restart the device (since the last time the device has stopped).
        /// For the very first attempt, this is 0. 
        /// </param>
        /// <returns>The number of millisecond to wait before the next retry or 0 to stop retrying.</returns>
        protected virtual Task<int> TryAlwaysRunningRestart( IActivityMonitor monitor, IDeviceAlwaysRunningPolicy global, IDevice device, int retryCount )
        {
            return global.RetryStartAsync( monitor, this, device, retryCount );
        }

        void IInternalDeviceHost.SetDaemon( DeviceHostDaemon daemon )
        {
            Debug.Assert( daemon != null );
            _daemon = daemon;
        }

        void IInternalDeviceHost.OnAlwaysRunningCheck( IDevice d, IActivityMonitor monitor )
        {
            using( monitor.OpenDebug( $"OnAlwaysRunningCheck for device '{d}'." ) )
            {
                lock( _alwayRunningStopped )
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
        }

        async ValueTask<long> IInternalDeviceHost.DaemonCheckAlwaysRunningAsync( IActivityMonitor monitor, IDeviceAlwaysRunningPolicy global, DateTime now )
        {
            // Fast path: nothing to do (hence the ValueTask).
            var devices = _alwayRunningStoppedSafe;
            if( devices.Length == 0 ) return Int64.MaxValue;

            // Slow path: Check times and call Policy.RetryStartAsync if needed.
            //            We work on the current devices array that has been captured (_alwayRunningStoppedSafe), no lock needed
            //            and this is for the best: we may call the async policy RetryStartAsync, that prevents us to use a sync lock.
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
                            delta = await TryAlwaysRunningRestart( monitor, global, d, e.Count ).ConfigureAwait( false );
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
                monitor.Fatal( "Unexpected error during AlwaysRunning retry.", ex );
                return Int32.MaxValue;
            }
            // Take the lock to update the _alwayRunningStopped list by merging it with the updatedDeltas.
            bool changed = false;
            long minDelta = Int64.MaxValue;
            lock( _alwayRunningStopped )
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

        void CaptureAlwayRunningStoppedSafe( IActivityMonitor monitor, bool signalHost )
        {
            Debug.Assert( System.Threading.Monitor.IsEntered( _alwayRunningStopped ) );
            _alwayRunningStoppedSafe = _alwayRunningStopped.ToArray();
            monitor.Debug( $"{(signalHost ? "Host signaled!" : "(no signal.)")}. Updated Always Running Stopped list of '{DeviceHostName}': ({_alwayRunningStoppedSafe.Select( e => $"{e.Device.Name}, {e.Count}, { e.NextCall.ToString( "HH:mm.ss.ff" )})" ).Concatenate( ", (" )})." );
            if( signalHost ) _daemon?.Signal();
        }
    }


}
