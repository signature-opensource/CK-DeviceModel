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
    /// Specialized <see cref="Device{TConfiguration}"/> that implements an independent event loop
    /// with its own monitor and can raise events thanks to its <see cref="DeviceEvent"/>.
    /// </summary>
    /// <typeparam name="TConfiguration">The device's configuration type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    public abstract partial class ActiveDevice<TConfiguration,TEvent> : Device<TConfiguration>, IActiveDevice, ActiveDevice<TConfiguration,TEvent>.IEventLoop
        where TConfiguration : DeviceConfiguration
        where TEvent : BaseDeviceEvent
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
            _deviceEvent = new PerfectEventSender<TEvent>();
            _allEvent = new PerfectEventSender<BaseDeviceEvent>();
            _ = Task.Run( RunEventLoop );
        }

        /// <summary>
        /// Central event for all device specific events.
        /// These events should not overlap with existing device's <see cref="Device{TConfiguration}.LifetimeEvent"/>.
        /// </summary>
        public PerfectEvent<TEvent> DeviceEvent => _deviceEvent.PerfectEvent;

        /// <inheritdoc />
        public PerfectEvent<BaseDeviceEvent> AllEvent => _allEvent.PerfectEvent;

        private protected override Task SafeRaiseLifetimeEventAsync( DeviceLifetimeEvent e )
        {
            DoPost( e );
            return base.SafeRaiseLifetimeEventAsync( e );
        }

        /// <summary>
        /// Posts an event in this device's event queue.
        /// This can be used to mimic a running device.
        /// </summary>
        /// <param name="e">The event to inject.</param>
        public void DebugPostEvent( TEvent e ) => DoPost( e );

        /// <summary>
        /// Gets the event loop API that implementation can use to execute 
        /// actions and logs to the event loop.
        /// </summary>
        protected IEventLoop EventLoop => this;

        void DoPost( object o ) => _events.Writer.TryWrite( o );
        void DoPost( Action<IActivityMonitor> o ) => _events.Writer.TryWrite( o );
        void DoPost( Func<IActivityMonitor, Task> o ) => _events.Writer.TryWrite( o );

        void IEventLoop.RaiseEvent( TEvent e ) => DoPost( e );
        void IEventLoop.Execute( Action<IActivityMonitor> action ) => DoPost( action );
        void IEventLoop.Execute( Func<IActivityMonitor,Task> action ) => DoPost( action );
        void IEventLoop.LogError( string msg ) => DoPost( m => m.Error( msg ) );
        void IEventLoop.LogError( string msg, Exception ex ) => DoPost( m => m.Error( msg, ex ) );
        void IEventLoop.LogWarn( string msg ) => DoPost( m => m.Warn( msg ) );
        void IEventLoop.LogWarn( string msg, Exception ex ) => DoPost( m => m.Warn( msg, ex ) );
        void IEventLoop.LogInfo( string msg ) => DoPost( m => m.Info( msg ) );
        void IEventLoop.LogTrace( string msg ) => DoPost( m => m.Trace( msg ) );
        void IEventLoop.LogDebug( string msg ) => DoPost( m => m.Debug( msg ) );

        async Task RunEventLoop()
        {
            var r = _events.Reader;
            object? ev = null;
            while( !IsDestroyed )
            {
                try
                {
                    ev = await r.ReadAsync( DestroyedToken ).ConfigureAwait( false );
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
                            await _deviceEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
                            await _allEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
                            break;
                        case DeviceLifetimeEvent e:
                            await _allEvent.SafeRaiseAsync( _eventMonitor, e ).ConfigureAwait( false );
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
                        _eventMonitor.Error( $"While processing event '{ev}'", ex );
                    }
                }
            }
            _eventMonitor.MonitorEnd();
        }
    }
}
