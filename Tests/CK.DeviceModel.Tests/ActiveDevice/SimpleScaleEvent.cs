using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    /// <summary>
    /// Base class for all SimpleScale events.
    /// SimpleScale events have a readable ToString().
    /// </summary>
    public abstract class SimpleScaleEvent
    {
        readonly string _text;

        private protected SimpleScaleEvent( string text )
        {
            _text = text;
        }

        public override string ToString() => _text;
    }
}
