using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    public partial class ActiveDevice<TConfiguration, TEvent>
    {
        /// <summary>
        /// Models the event loop API available inside an ActiveDevice.
        /// </summary>
        public interface IEventLoop : IMonitoredWorker
        {
            /// <summary>
            /// Sends a device event into <see cref="DeviceEvent"/>.
            /// </summary>
            /// <param name="e">The event to send.</param>
            void RaiseEvent( TEvent e );
        }
    }
}
