using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel;

/// <summary>
/// Non generic base for <see cref="StartDeviceCommand{THost}"/> command that
/// attempts to start a device.
/// </summary>
/// <remarks>
/// This class cannot be specialized. The only concrete type of this command is <see cref="StartDeviceCommand{THost}"/>.
/// </remarks>
public abstract class BaseStartDeviceCommand : DeviceCommandWithResult<bool>
{
    private protected BaseStartDeviceCommand()
    {
        ImmediateSending = true;
        ShouldCallDeviceOnCommandCompleted = false;
    }

    /// <summary>
    /// Transforms any error into false result.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="result">The result setter.</param>
    protected override sealed void OnError( Exception ex, ref CompletionSource<bool>.OnError result )
    {
        result.SetResult( false );
    }

    /// <summary>
    /// Transforms cancellation into false result.
    /// </summary>
    /// <param name="result">The result setter.</param>
    protected override sealed void OnCanceled( ref CompletionSource<bool>.OnCanceled result )
    {
        result.SetResult( false );
    }

    /// <summary>
    /// Obviously returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>.
    /// Note that this is not used: basic commands are always run by design.
    /// </summary>
    protected internal sealed override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    /// <summary>
    /// Obviously returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/>.
    /// Note that this is not used: basic commands are always run by design.
    /// </summary>
    protected internal sealed override DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

}
