using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
        readonly List<(IInternalDevice Device, int Count, DateTime NextCall)> _alwayRunningStopped;
        DeviceHostDaemon? _daemon;
        volatile bool _daemonCheckRequired;

        /// <summary>
        /// Gets the <see cref="DeviceHostDaemon.StoppedToken"/>.
        /// </summary>
        public CancellationToken DaemonStoppedToken => _daemon != null ? _daemon.StoppedToken : CancellationToken.None;


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
        protected virtual Task<int> TryAlwaysRunningRestartAsync( IActivityMonitor monitor, IDeviceAlwaysRunningPolicy global, IDevice device, int retryCount )
        {
            monitor.Trace( $"Using '{global.GetType().Name}' global running policy." );
            return global.RetryStartAsync( monitor, this, device, retryCount );
        }

        void IInternalDeviceHost.SetDaemon( DeviceHostDaemon daemon )
        {
            Debug.Assert( daemon != null );
            _daemon = daemon;
        }

        void IInternalDeviceHost.DeviceOnAlwaysRunningCheck( IInternalDevice d, IActivityMonitor monitor )
        {
            using( monitor.OpenDebug( $"OnAlwaysRunningCheck for device '{d}'." ) )
            {
                lock( _alwayRunningStopped )
                {
                    int idx = _alwayRunningStopped.IndexOf( e => e.Device == d );
                    if( idx >= 0 )
                    {
                        if( d.IsRunning || d.ConfigStatus != DeviceConfigurationStatus.AlwaysRunning )
                        {
                            monitor.Debug( "Removing Device from Always Running Stopped list." );
                            _alwayRunningStopped.RemoveAt( idx );
                        }
                        else
                        {
                            Debug.Assert( d.ConfigStatus == DeviceConfigurationStatus.AlwaysRunning );
                            monitor.Debug( "Updated Device retry count in Always Running Stopped list." );
                            // Let the NextCall unchanged: manual Starts are ignored.
                            _alwayRunningStopped[idx] = (d, _alwayRunningStopped[idx].Count + 1, _alwayRunningStopped[idx].NextCall);
                        }
                    }
                    else
                    {
                        if( !d.IsRunning && d.ConfigStatus == DeviceConfigurationStatus.AlwaysRunning )
                        {
                            monitor.Debug( "Adding Device to Always Running Stopped list and signaling the Daemon." );
                            _daemonCheckRequired = true;
                            // Next call (Count = 0) is asap.
                            _alwayRunningStopped.Add( (d, 0, DateTime.UtcNow) );
                            _daemon?.Signal();
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
            if( !_daemonCheckRequired ) return Int64.MaxValue;

            (IInternalDevice Device, int Count, DateTime NextCall)[] copy;
            lock( _alwayRunningStopped )
            {
                copy = _alwayRunningStopped.ToArray();
            }
            monitor.Debug( $"Handling Always Running Stopped list of '{DeviceHostName}' (Count: {copy.Length}): ({copy.Select( e => $"{e.Device.Name}, {e.Count}, { e.NextCall.ToString( "HH:mm.ss.ff" )})" ).Concatenate( ", (" )})." );

            // Check times and call RetryStartAsync if needed outside of any lock: since we may call the async RetryStartAsync,
            // that prevents us to use a sync lock and any lock here may lead to deadlocks.

            long[] updatedDeltas = new long[copy.Length];
            try
            {
                int idx = 0;
                foreach( var e in copy )
                {
                    var d = e.Device;
                    // Delta 0 means: the device should not be anymore in _alwayRunningStopped list (we'll ignore it).
                    long delta = 0;
                    if( !d.IsRunning && d.ConfigStatus == DeviceConfigurationStatus.AlwaysRunning )
                    {
                        delta = (e.NextCall - now).Ticks;
                        if( delta <= 0 )
                        {
                            using( monitor.OpenInfo( $"Attempt n°{e.Count} to restart the device '{d.FullName}'." ) )
                            {
                                delta = await TryAlwaysRunningRestartAsync( monitor, global, d, e.Count ).ConfigureAwait( false );
                                if( d.IsRunning || delta < 0 )
                                {
                                    monitor.CloseGroup( $"Successfully restarted." );
                                    delta = 0;
                                }
                                else
                                {
                                    if( delta == 0 )
                                    {
                                        monitor.CloseGroup( $"Restart failed. No more retries will be done." );
                                    }
                                    else
                                    {
                                        monitor.CloseGroup( $"Restart failed. Retrying in {delta} ms." );
                                        delta *= TimeSpan.TicksPerMillisecond;
                                        // Delta is positive: means that the NextCallDate in the _alwayRunningStopped list must be updated.
                                    }
                                }
                            }
                        }
                        else
                        {
                            // When Delta is negative, it means that the device must keep its NextCallDate in the _alwayRunningStopped list.
                            delta = -delta;
                        }
                    }
                    else
                    {
                        monitor.Debug( $"Device '{d.FullName}' is running or its ConfigStatus is no more DeviceConfigurationStatus.AlwaysRunning. It is ignored." );
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
            long minDelta = Int64.MaxValue;
            lock( _alwayRunningStopped )
            {
                for( int i = 0; i < copy.Length; i++ )
                {
                    var (d,count,nextCall) = copy[i];
                    int existIdx = _alwayRunningStopped.IndexOf( e => e.Device == d );
                    if( existIdx >= 0 )
                    {
                        var delta = updatedDeltas[i];
                        // It doesn't cost much to check if, right now, the device is still in trouble.
                        if( delta != 0 && !d.IsRunning && d.ConfigStatus == DeviceConfigurationStatus.AlwaysRunning )
                        {
                            if( delta > 0 )
                            {
                                _alwayRunningStopped[existIdx] = (d, _alwayRunningStopped[existIdx].Count, now.AddTicks( delta ));
                            }
                            else
                            {
                                // Not touched.
                                delta = -delta;
                                Debug.Assert( delta > 0 );
                            }
                            if( minDelta > delta ) minDelta = delta;
                        }
                    }
                }
            }
            return minDelta;
        }
    }


}
