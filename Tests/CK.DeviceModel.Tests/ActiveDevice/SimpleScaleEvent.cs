using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Base class for all SimpleScale events.
    /// SimpleScale events have a readable ToString().
    /// </summary>
    public abstract class SimpleScaleEvent : ActiveDeviceEvent<SimpleScale>
    {
        readonly string _text;

        private protected SimpleScaleEvent( SimpleScale device, string text )
            : base( device )
        {
            _text = text;
        }

        public override string ToString() => _text;
    }
}
