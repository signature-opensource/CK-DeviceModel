namespace CK.DeviceModel;

public partial class ActiveDevice<TConfiguration, TEvent>
{
    /// <summary>
    /// Models the event loop API available inside an ActiveDevice.
    /// Extends the non generic <see cref="DeviceModel.IEventLoop"/> with <typeparamref name="TEvent"/>
    /// typed RaiseEvent.
    /// </summary>
    public interface IEventLoop : DeviceModel.IEventLoop
    {
        /// <summary>
        /// Sends a device event into <see cref="DeviceEvent"/> and <see cref="AllEvent"/>.
        /// </summary>
        /// <param name="e">The event to send.</param>
        /// <returns>The event.</returns>
        TEvent RaiseEvent( TEvent e );
    }
}
