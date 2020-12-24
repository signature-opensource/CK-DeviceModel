using CK.Core;
using CK.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// This is the "restarter daemon" that allows devices with <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration
    /// to be monitored and automatically restarted when stopped. See <see cref="IDeviceAlwaysRunningPolicy"/>.
    /// </summary>
    public sealed class DeviceHostDaemon : ISingletonAutoService, IHostedService
    {
        readonly IInternalDeviceHost[] _deviceHosts;
        readonly CancellationTokenSource _run;

        volatile TaskCompletionSource<bool> _signal;
        IActivityMonitor? _daemonMonitor;

        /// <summary>
        /// Initializes a new <see cref="DeviceHostDaemon"/>.
        /// </summary>
        /// <param name="deviceHosts">The available hosts.</param>
        public DeviceHostDaemon( IEnumerable<IDeviceHost> deviceHosts )
        {
            _run = new CancellationTokenSource();
            _signal = new TaskCompletionSource<bool>();
           _deviceHosts = deviceHosts.Cast<IInternalDeviceHost>().ToArray();
        }

        Task IHostedService.StartAsync( CancellationToken cancellationToken )
        {
            if( _deviceHosts.Length > 0 )
            {
                _daemonMonitor = new ActivityMonitor( nameof( DeviceHostDaemon ) );
                // We don't wait for the actual termination of the loop: we don't need to capture the loop task.
                _ = Task.Run( TheLoop );
            }
            return Task.CompletedTask;
        }


        internal void Signal()
        {
            var s = _signal;
            _signal = new TaskCompletionSource<bool>();
            // This ensures that the Loop doesn't run on this thread. 
            Task.Run( () => s.TrySetResult( true ) );
        }

        async Task TheLoop()
        {
            Debug.Assert( _daemonMonitor != null );
            foreach( var h in _deviceHosts )
            {
                h.SetDaemon( this );
            }
            _daemonMonitor.Debug( "Daemon loop started." );
            while( !_run.IsCancellationRequested )
            {
                var signalTask = _signal.Task;
                using( _daemonMonitor.OpenDebug( "Checking always running devices." ) )
                {
                    DateTime now = DateTime.UtcNow;
                    long wait = Int64.MaxValue;
                    foreach( var h in _deviceHosts )
                    {
                        var delta = await h.CheckAlwaysRunningAsync( _daemonMonitor, now );
                        Debug.Assert( delta > 0 );
                        if( wait > delta ) wait = delta;
                    }
                    if( signalTask.IsCompleted )
                    {
                        _daemonMonitor.CloseGroup( $"Host has been signaled. Repeat." );
                    }
                    else if( wait != Int64.MaxValue )
                    {
                        int w = (int)(wait / TimeSpan.TicksPerMillisecond);
                        _daemonMonitor.CloseGroup( $"Waiting for {w} ms or a host's signal." );
                        await Task.WhenAny( Task.Delay( w ), signalTask );
                    }
                    else
                    {
                        _daemonMonitor.CloseGroup( $"Waiting for a host's signal." );
                        await signalTask;
                    }
                }
            }
        }

        Task IHostedService.StopAsync( CancellationToken cancellationToken )
        {
            if( _deviceHosts.Length > 0 )
            {
                _run.Cancel();
                _daemonMonitor.MonitorEnd();
            }
            return Task.CompletedTask;
        }
    }
}
