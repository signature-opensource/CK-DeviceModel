using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel.Tests
{
    public class SimpleScaleConfiguration : DeviceConfiguration
    {
        public SimpleScaleConfiguration()
        {
        }

        public int PhysicalRate { get; set; } = 20;

        public int MeasureStep { get; set; } = 10;

        public string? MeasurePattern { get; set; }

        public bool ResetOnStart { get; set; }

        /// <summary>
        /// Gets or sets whether when we receive a negative value, the
        /// device must stop.
        /// </summary>
        public bool StopOnNegativeValue { get; set; }

        /// <summary>
        /// Gets or sets whether after a <see cref="StopOnNegativeValue"/>, a call to
        /// StartAsync is done automatically 10*PhysicalRate ms after the stop from the event loop.
        /// </summary>
        public bool AllowUnattendedRestartAfterStopOnNegativeValue { get; set; }

        public SimpleScaleConfiguration( SimpleScaleConfiguration o )
            : base( o )
        {
            PhysicalRate = o.PhysicalRate;
            MeasureStep = o.MeasureStep;
            MeasurePattern = o.MeasurePattern;
            ResetOnStart = o.ResetOnStart;
            StopOnNegativeValue = o.StopOnNegativeValue;
            AllowUnattendedRestartAfterStopOnNegativeValue = o.AllowUnattendedRestartAfterStopOnNegativeValue;
        }


        public SimpleScaleConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version
            PhysicalRate = r.ReadInt32();
            MeasureStep = r.ReadInt32();
            MeasurePattern = r.ReadNullableString();
            ResetOnStart = r.ReadBoolean();
            StopOnNegativeValue = r.ReadBoolean();
            AllowUnattendedRestartAfterStopOnNegativeValue = r.ReadBoolean();
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.Write( PhysicalRate );
            w.Write( MeasureStep );
            w.WriteNullableString( MeasurePattern );
            w.Write( ResetOnStart );
            w.Write( StopOnNegativeValue );
            w.Write( AllowUnattendedRestartAfterStopOnNegativeValue );
        }
    }
}
