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
    /// and can raise events thanks to its <see cref="AllEvent"/>.
    /// </summary>
    /// <typeparam name="TConfiguration">The device's configuration type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    public abstract class ActiveDevice<TConfiguration,TEvent> : Device<TConfiguration>
        where TConfiguration : DeviceConfiguration
        where TEvent : class
    {
        readonly Channel<object> _events;
        readonly ActivityMonitor _eventMonitor;
        readonly PerfectEventSender<IDevice,TEvent> _allEvents;


        /// <inheritdoc />
        public ActiveDevice( IActivityMonitor monitor, CreateInfo info )
            : base( monitor, info )
        {
            _events = Channel.CreateUnbounded<object>( new UnboundedChannelOptions() { SingleReader = true } );
            _eventMonitor = new ActivityMonitor( $"Event loop for device {FullName}." );
            _allEvents = new PerfectEventSender<IDevice, TEvent>();
            _ = Task.Run( EventLoop );
        }

        /// <summary>
        /// Central event for all device specific events.
        /// These events should not overlap with existing device's lifetime events <see cref="Device{TConfiguration}.StatusChanged"/>
        /// and <see cref="Device{TConfiguration}.ControllerKeyChanged"/>.
        /// </summary>
        public PerfectEvent<IDevice, TEvent> AllEvent => _allEvents.PerfectEvent;

        /// <summary>
        /// Posts an event in this device's event queue.
        /// This can be used to mimic a running device.
        /// </summary>
        /// <param name="e">The event to inject.</param>
        public void DebugPostEvent( TEvent e ) => PostEvent( e );

        /// <summary>
        /// Sends an event into <see cref="AllEvent"/>.
        /// </summary>
        /// <param name="e">The event to send.</param>
        protected void PostEvent( TEvent e ) => _events.Writer.TryWrite( e );

        /// <summary>
        /// Posts the given action to be executed on the event loop.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        protected void PostAction( Action<IActivityMonitor> action ) => _events.Writer.TryWrite( action );

        /// <summary>
        /// Posts an error log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        protected void LogError( string msg ) => PostAction( m => m.Error( msg ) );

        /// <summary>
        /// Posts an error log message with an exception into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        protected void LogError( string msg, Exception ex ) => PostAction( m => m.Error( msg, ex ) );

        /// <summary>
        /// Posts a warning log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        protected void LogWarn( string msg ) => PostAction( m => m.Warn( msg ) );

        /// <summary>
        /// Posts a warning log message with an exception into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="ex">The exception to log.</param>
        protected void LogWarn( string msg, Exception ex ) => PostAction( m => m.Warn( msg, ex ) );

        /// <summary>
        /// Posts an informational message log into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        protected void LogInfo( string msg ) => PostAction( m => m.Info( msg ) );

        /// <summary>
        /// Posts a trace log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        protected void LogTrace( string msg ) => PostAction( m => m.Trace( msg ) );

        /// <summary>
        /// Posts a debug log message into the event monitor.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        protected void LogDebug( string msg ) => PostAction( m => m.Debug( msg ) );

        async Task EventLoop()
        {
            var r = _events.Reader;
            object? ev = null;
            while( !IsDestroyed )
            {
                try
                {
                    ev = await r.ReadAsync( DestroyedToken ).ConfigureAwait( false );
                    if( ev == null ) continue;
                    if( ev is Action<IActivityMonitor> action )
                    {
                        action( _eventMonitor );
                    }
                    else if( ev is TEvent e )
                    {
                        await _allEvents.SafeRaiseAsync( _eventMonitor, this, e ).ConfigureAwait( false );
                    }
                    else
                    {
                        _eventMonitor.Error( $"Unknown event type '{ev.GetType()}'." );
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
