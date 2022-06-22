using CK.Core;
using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// Dummy command that is used to awake the command loop.
    /// This does nothing and is ignored except that, just like any other commands that are
    /// dequeued, this triggers the _commandQueueImmediate execution.
    /// <para>
    /// It is the same instance for all the device type, this is why it is implemented
    /// outside of the generic Device&lt;TConfiguration&gt;.
    /// </para>
    /// </summary>
    sealed class CommandAwaker : BaseDeviceCommand
    {
        public static readonly CommandAwaker Instance = new();

        CommandAwaker() : base( (string.Empty, null) ) { }
        public override Type HostType => throw new NotImplementedException();
        internal override ICompletionSource InternalCompletion => throw new NotImplementedException();
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
        public override string ToString() => nameof( CommandAwaker );
    }
    
}
