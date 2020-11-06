using CK.Core;
using CK.Text;
using Microsoft.Extensions.Configuration;
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

        TaskCompletionSource<bool> _signal;
        IActivityMonitor? _daemonMonitor;

        /// <summary>
        /// Initializes a new <see cref="DeviceHostDaemon"/>.
        /// </summary>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="deviceHosts">The available hosts.</param>
        public DeviceHostDaemon( IConfiguration configuration, IEnumerable<IDeviceHost> deviceHosts )
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
            _signal.TrySetResult( true );
        }

        async Task TheLoop()
        {
            Debug.Assert( _daemonMonitor != null );
            foreach( var h in _deviceHosts )
            {
                h.SetDaemon( this );
            }
            while( !_run.IsCancellationRequested )
            {
                DateTime now = DateTime.UtcNow;
                int wait = Int32.MaxValue;
                foreach( var h in _deviceHosts )
                {
                    var delta = await h.CheckAlwaysRunningAsync( _daemonMonitor, now );
                    Debug.Assert( delta > 0 );
                    if( wait > delta ) wait = delta;
                }
                if( wait != Int32.MaxValue )
                {
                    await Task.WhenAny( Task.Delay( wait ), _signal.Task );
                }
                else
                {
                    await _signal.Task;
                }
                _signal = new TaskCompletionSource<bool>();
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
