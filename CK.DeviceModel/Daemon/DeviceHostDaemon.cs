using CK.Core;
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
    /// <para>
    /// This daemon can also destroy device when stopped (see <see cref="StoppedBehavior"/>).
    /// </para>
    /// </summary>
    public sealed class DeviceHostDaemon : ISingletonAutoService, IHostedService
    {
        readonly IInternalDeviceHost[] _deviceHosts;
        readonly IDeviceAlwaysRunningPolicy _alwaysRunningPolicy;
        readonly CancellationTokenSource _stoppedTokenSource;

        Task? _runLoop;
        volatile TaskCompletionSource<bool> _signal;
        IActivityMonitor? _daemonMonitor;

        /// <summary>
        /// Initializes a new <see cref="DeviceHostDaemon"/>.
        /// </summary>
        /// <param name="deviceHosts">The available device hosts.</param>
        /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
        public DeviceHostDaemon( IEnumerable<IDeviceHost> deviceHosts, IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
        {
            _alwaysRunningPolicy = alwaysRunningPolicy ?? throw new ArgumentNullException( nameof( alwaysRunningPolicy ) );
            _stoppedTokenSource = new CancellationTokenSource();
            // See here https://stackoverflow.com/questions/28321457/taskcontinuationoptions-runcontinuationsasynchronously-and-stack-dives
            // why RunContinuationsAsynchronously is crucial.
            _signal = new TaskCompletionSource<bool>( TaskCreationOptions.RunContinuationsAsynchronously );
           _deviceHosts = deviceHosts.Cast<IInternalDeviceHost>().ToArray();
            // Don't wait for the Start: associate the daemon right now to each and every hosts.
            foreach( var h in _deviceHosts )
            {
                h.SetDaemon( this );
            }
        }

        /// <summary>
        /// Gets or sets whether when this service stops, all devices of all hosts must be destroyed.
        /// Defaults to <see cref="OnStoppedDaemonBehavior.None"/>.
        /// </summary>
        public OnStoppedDaemonBehavior StoppedBehavior { get; set; }

        /// <summary>
        /// Gets a cancellation token that will be signaled when this service is stopping.
        /// </summary>
        public CancellationToken StoppedToken => _stoppedTokenSource.Token;

        Task IHostedService.StartAsync( CancellationToken cancellationToken )
        {
            if( _deviceHosts.Length > 0 )
            {
                _daemonMonitor = new ActivityMonitor( nameof( DeviceHostDaemon ) );
                _runLoop = TheLoop();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggers the current source after having initiated the next one.
        /// </summary>
        internal void Signal()
        {
            var s = _signal;
            _signal = new TaskCompletionSource<bool>( TaskCreationOptions.RunContinuationsAsynchronously );
            s.TrySetResult( true );
        }

        async Task TheLoop()
        {
            Debug.Assert( _daemonMonitor != null );
            _daemonMonitor.Debug( "Daemon loop started." );
            while( !_stoppedTokenSource.IsCancellationRequested )
            {
                var signalTask = _signal.Task;
                using( _daemonMonitor.OpenDebug( "Checking always running devices." ) )
                {
                    DateTime now = DateTime.UtcNow;
                    long wait = Int64.MaxValue;
                    foreach( var h in _deviceHosts )
                    {
                        var delta = await h.DaemonCheckAlwaysRunningAsync( _daemonMonitor, _alwaysRunningPolicy, now ).ConfigureAwait( false );
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
                        await Task.WhenAny( Task.Delay( w ), signalTask ).ConfigureAwait( false );
                    }
                    else
                    {
                        _daemonMonitor.CloseGroup( $"Waiting for a host's signal." );
                        await signalTask.ConfigureAwait( false );
                    }
                }
            }
            if( StoppedBehavior != OnStoppedDaemonBehavior.None )
            {
                using( _daemonMonitor.OpenInfo( $"End of Daemon loop: StoppedBehavior is '{StoppedBehavior}'." ) )
                {
                    // We may catch a OperationCanceledException here.
                    try
                    {
                        if( StoppedBehavior == OnStoppedDaemonBehavior.ClearAllHosts )
                        {
                            foreach( var h in _deviceHosts )
                            {
                                await h.ClearAsync( _daemonMonitor, waitForDeviceDestroyed: false ).ConfigureAwait( false );
                            }
                        }
                        else
                        {
                            Task[] all = new Task[_deviceHosts.Length];
                            for( int i = 0; i < _deviceHosts.Length; ++i )
                            {
                                all[i] = _deviceHosts[i].ClearAsync( _daemonMonitor, waitForDeviceDestroyed: true );
                            }
                            await Task.WhenAll( all ).ConfigureAwait( false );
                        }
                    }
                    catch( Exception ex )
                    {
                        _daemonMonitor.Error( ex );
                    }
                }
            }
            _daemonMonitor.MonitorEnd();
        }

        async Task IHostedService.StopAsync( CancellationToken cancellationToken )
        {
            if( _deviceHosts.Length > 0 )
            {
                Debug.Assert( _runLoop != null );
                _stoppedTokenSource.Cancel();
                Signal();
                await _runLoop.WaitAsync( Timeout.Infinite, cancellationToken );
            }
        }
    }
}
