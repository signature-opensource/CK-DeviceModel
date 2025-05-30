using CK.Core;
using CK.PerfectEvent;
using System;
using System.Threading.Tasks;

namespace CK.DeviceModel;

/// <summary>
/// Specialized <see cref="Device{TConfiguration}"/> that exposes <see cref="DeviceEvent"/> and <see cref="AllEvent"/>
/// and supports <see cref="IActiveDevice{TEvent}"/>.
/// <para>
/// This SimpleActiveDevice raises its <see cref="AllEvent"/> from the command loop instead of running
/// a dedicated event loop like the "real" <see cref="ActiveDevice{TConfiguration, TEvent}"/>.
/// </para>
/// </summary>
/// <typeparam name="TConfiguration">The device's configuration type.</typeparam>
/// <typeparam name="TEvent">
/// The event type.
/// Each active device should be associated to a specialized <see cref="ActiveDeviceEvent{TDevice}"/>
/// </typeparam>
public abstract partial class SimpleActiveDevice<TConfiguration, TEvent> : Device<TConfiguration>, IActiveDevice<TEvent>
    where TConfiguration : DeviceConfiguration, new()
    where TEvent : BaseActiveDeviceEvent
{
    readonly PerfectEventSender<TEvent> _deviceEvent;
    readonly PerfectEventSender<BaseDeviceEvent> _allEvent;

    /// <inheritdoc />
    public SimpleActiveDevice( IActivityMonitor monitor, CreateInfo info )
        : base( monitor, info )
    {
        _deviceEvent = new PerfectEventSender<TEvent>();
        _allEvent = new PerfectEventSender<BaseDeviceEvent>();
    }

    /// <summary>
    /// Central event for all device specific events.
    /// These events should not overlap with existing device's <see cref="Device{TConfiguration}.LifetimeEvent"/>.
    /// </summary>
    public PerfectEvent<TEvent> DeviceEvent => _deviceEvent.PerfectEvent;

    /// <inheritdoc />
    public PerfectEvent<BaseDeviceEvent> AllEvent => _allEvent.PerfectEvent;

    private protected override sealed Task OnSafeRaiseLifetimeEventAsync( IActivityMonitor monitor, DeviceLifetimeEvent<TConfiguration> e )
    {
        // Raises this AllEvent and the host AllDevicesEvent.
        // Order should not matter here.
        return _allEvent.SafeRaiseAsync( monitor, e )
               .ContinueWith( _ => _host.RaiseAllDevicesEventAsync( monitor, e ), TaskScheduler.Default )
               .Unwrap();
    }

    /// <summary>
    /// Dummy command that carries the event to send to the event loop.
    /// </summary>
    sealed class AutoSendEvent : DeviceCommandNoResult
    {
        public readonly TEvent Event;

        public AutoSendEvent( TEvent e )
        {
            ImmediateSending = true;
            ShouldCallDeviceOnCommandCompleted = false;
            Event = e;
            // Immediate, RunAnyway and completion is not set.
            // We can leave it unset, it will be set to "WaitForCompletion" by 
            // HandleCommandAsync but this is useless (and we don't want this to be tracked
            // as a long running command).
            TrySetLongRunningReason( null );
        }

        public override Type HostType => throw new NotImplementedException( "Never called." );

        protected internal override DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;
    }

    /// <summary>
    /// Sends an internal immediate command with <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/> that carries the
    /// event into the command loop.
    /// <para>
    /// To raise events while handling a command, the protected <see cref="RaiseEventAsync(IActivityMonitor, TEvent)"/> should
    /// be used. However, even if this implements the <see cref="IActiveDevice.DebugPostEvent(BaseActiveDeviceEvent)"/> contract for
    /// the external world, this can also be used internally to defer the event raising. 
    /// </para>
    /// </summary>
    /// <param name="e">
    /// The event to raise. The event's type must match the actual <see cref="ActiveDeviceEvent{TDevice}"/> type
    /// otherwise an <see cref="InvalidCastException"/> is thrown.
    /// </param>
    public void DebugPostEvent( BaseActiveDeviceEvent e ) => SendRoutedCommandImmediate( new AutoSendEvent( (TEvent)e ) );


    /// <summary>
    /// Raises a <see cref="DeviceEvent"/>.
    /// This MUST always be awaited.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="e">The device event to raise.</param>
    /// <returns>The awaitable.</returns>
    protected Task RaiseEventAsync( IActivityMonitor monitor, TEvent e )
    {
        return _deviceEvent.SafeRaiseAsync( monitor, e )
                           .ContinueWith( _ => _allEvent.SafeRaiseAsync( monitor, e ), TaskScheduler.Default )
                           .Unwrap();
    }

    /// <summary>
    /// Overridden to handle the internal command that raises events from <see cref="IActiveDevice.DebugPostEvent(BaseActiveDeviceEvent)"/> or
    /// calls the base <see cref="Device{TConfiguration}.DoHandleCommandAsync(IActivityMonitor, BaseDeviceCommand)"/> (that
    /// throws an <see cref="NotSupportedException"/>).
    /// </summary>
    /// <param name="monitor">The command monitor.</param>
    /// <param name="command">The command to handle.</param>
    /// <returns>The awaitable.</returns>
    protected override Task DoHandleCommandAsync( IActivityMonitor monitor, BaseDeviceCommand command )
    {
        return command is AutoSendEvent s
                ? RaiseEventAsync( monitor, s.Event )
                : base.DoHandleCommandAsync( monitor, command );
    }

}
