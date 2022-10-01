using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Specialized <see cref="Device{TConfiguration}"/> that supports <see cref="IActiveDevice{TEvent}"/> and
    /// implements an independent event loop with its own monitor.
    /// </summary>
    /// <typeparam name="TConfiguration">The device's configuration type.</typeparam>
    /// <typeparam name="TEvent">
    /// The event type.
    /// Each active device should be associated to a specialized <see cref="ActiveDeviceEvent{TDevice}"/>
    /// </typeparam>
    public abstract partial class ActiveDevice<TConfiguration,TEvent> : Device<TConfiguration>,
                                                                        IActiveDevice<TEvent>,
                                                                        ActiveDevice<TConfiguration,TEvent>.IEventLoop
        where TConfiguration : DeviceConfiguration
        where TEvent : BaseActiveDeviceEvent
    {
        readonly Channel<object> _events;
        readonly ActivityMonitor _eventMonitor;
        readonly PerfectEventSender<TEvent> _deviceEvent;
        readonly PerfectEventSender<BaseDeviceEvent> _allEvent;

        /// <inheritdoc />
        public ActiveDevice( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            _events = Channel.CreateUnbounded<object>( new UnboundedChannelOptions() { SingleReader = true } );
            _eventMonitor = new ActivityMonitor( $"Event loop for device {FullName}." );
            _eventMonitor.AutoTags += IDeviceHost.DeviceModel;

            _deviceEvent = new PerfectEventSender<TEvent>();
            _allEvent = new PerfectEventSender<BaseDeviceEvent>();
            _ = Task.Run( RunEventLoopAsync );
        }

        /// <summary>
        /// Central event for all device specific events.
        /// These events should not overlap with existing device's <see cref="Device{TConfiguration}.LifetimeEvent"/>.
        /// </summary>
        public PerfectEvent<TEvent> DeviceEvent => _deviceEvent.PerfectEvent;

        /// <inheritdoc />
        /// <remarks>
        /// Events are always raised on the event loop: LifeTimeEvent are marshalled to the
        /// internal event channel so that this stream of all events (for one device of course),
        /// is guaranteed to be serialized.
        /// </remarks>
        public PerfectEvent<BaseDeviceEvent> AllEvent => _allEvent.PerfectEvent;

        private protected override sealed Task OnSafeRaiseLifetimeEventAsync( IActivityMonitor monitor, DeviceLifetimeEvent<TConfiguration> e )
        {
            DoPost( e );
            return Task.CompletedTask;
        }

        void IActiveDevice.DebugPostEvent( BaseActiveDeviceEvent e ) => DoPost( (TEvent)e );

        /// <summary>
        /// Posts an event in this device's event queue.
        /// This should be used with care and can be used to mimic a running device.
        /// </summary>
        /// <param name="e">The event to inject.</param>
        public void DebugPostEvent( TEvent e ) => DoPost( e );

        /// <summary>
        /// Gets the event loop API that implementation can use to safely execute 
        /// actions, send logs to the event loop or call <see cref="IEventLoop.RaiseEvent(TEvent)"/>.
        /// <para>
        /// Always check that the executed action doesn't use any of the <see cref="Device{TConfiguration}.CommandLoop"/>
        /// resources since these 2 loops execute concurrently.
        /// </para>
        /// </summary>
        protected IEventLoop EventLoop => this;

        /// <summary>
        /// Raises a device event from inside the event loop if the monitor is the one of the
        /// event loop, otherwise posts the event to the loop.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="e">The event to send.</param>
        /// <returns>The awaitable.</returns>
        protected Task RaiseEventAsync( IActivityMonitor monitor, TEvent e )
        {
            if( IsInEventLoop( monitor ) )
            {
                return DoRaiseEventAsync( e );
            }
            DoPost( e );
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets whether the current activity is executing in the event loop.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <returns>True if the monitor is the event loop monitor, false otherwise.</returns>
        protected bool IsInEventLoop( IActivityMonitor monitor ) => monitor == _eventMonitor;

        void DoPost( object o ) => _events.Writer.TryWrite( o );
        void DoPost( Action<IActivityMonitor> o ) => _events.Writer.TryWrite( o );
        void DoPost( Func<IActivityMonitor, Task> o ) => _events.Writer.TryWrite( o );

        TEvent IEventLoop.RaiseEvent( TEvent e )
        {
            DoPost( e );
            return e;
        }

        void IMonitoredWorker.Execute( Action<IActivityMonitor> action ) => DoPost( action );
        void IMonitoredWorker.Execute( Func<IActivityMonitor,Task> action ) => DoPost( action );
        void IMonitoredWorker.LogError( string msg ) => DoPost( m => m.Error( msg ) );
        void IMonitoredWorker.LogError( string msg, Exception ex ) => DoPost( m => m.Error( msg, ex ) );
        void IMonitoredWorker.LogWarn( string msg ) => DoPost( m => m.Warn( msg ) );
        void IMonitoredWorker.LogWarn( string msg, Exception ex ) => DoPost( m => m.Warn( msg, ex ) );
        void IMonitoredWorker.LogInfo( string msg ) => DoPost( m => m.Info( msg ) );
        void IMonitoredWorker.LogTrace( string msg ) => DoPost( m => m.Trace( msg ) );
        void IMonitoredWorker.LogDebug( string msg ) => DoPost( m => m.Debug( msg ) );

        async Task RunEventLoopAsync()
        {
            var r = _events.Reader;
            object? ev = null;
            bool receivedDestroyed = false;
            while( !receivedDestroyed )
            {
                try
                {
                    // The lifetime event with a status IsDetroyed end this loop: we don't need a cancellation token.
                    // This allows the SingleConsumerUnboundedChannel<T>.UnboundedChannelReader
                    // to use a pooled IValueTaskSource instead of creating a new one. 
                    ev = await r.ReadAsync().ConfigureAwait( false );
                    switch( ev )
                    {
                        case null: continue;
                        case Action<IActivityMonitor> a:
                            a( _eventMonitor );
                            break;
                        case Func<IActivityMonitor, Task> a:
                            await a( _eventMonitor ).ConfigureAwait( false );
                            break;
                        case TEvent e:
                            await DoRaiseEventAsync( e ).ConfigureAwait( false );
                            break;
                        case DeviceLifetimeEvent e:
                            await _allEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
                            await _host.RaiseAllDevicesEventAsync( _eventMonitor, e ).ConfigureAwait( false );
                            if( e.DeviceStatus.IsDestroyed ) receivedDestroyed = true;
                            break;
                        default:
                            _eventMonitor.Error( $"Unknown event type '{ev.GetType()}'." );
                            break;
                    }
                }
                catch( Exception ex )
                {
                    // We always log except if this is a canceled exception and we have been destroyed.
                    if( ex is not OperationCanceledException || !IsDestroyed )
                    {
                        _eventMonitor.Error( $"While processing event '{ev}'.", ex );
                    }
                }
            }
            _eventMonitor.MonitorEnd();
        }

        async Task DoRaiseEventAsync( TEvent e )
        {
            await _deviceEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
            await _allEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
            await _host.RaiseAllDevicesEventAsync( _eventMonitor, e ).ConfigureAwait( false );
        }
    }
}
