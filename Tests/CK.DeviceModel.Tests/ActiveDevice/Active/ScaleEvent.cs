using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Base class for all Scale events.
    /// Scale events have a readable ToString().
    /// </summary>
    public abstract class ScaleEvent : ActiveDeviceEvent<Scale>
    {
        readonly string _text;

        private protected ScaleEvent( Scale device, string text )
            : base( device )
        {
            _text = text;
        }

        public override string ToString() => _text;
    }
}
