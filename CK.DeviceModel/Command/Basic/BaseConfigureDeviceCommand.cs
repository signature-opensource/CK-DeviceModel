using CK.Core;
using System;

namespace CK.DeviceModel;

/// <summary>
/// Non-generic base for the generic <see cref="ConfigureDeviceCommand{THost,TConfiguration}"/> (that
/// is the only one that can be used).
/// It exposes a base <see cref="DeviceConfiguration"/> and is useful when configuration type
/// is not statically available.
/// </summary>
public abstract class BaseConfigureDeviceCommand : DeviceCommandWithResult<DeviceApplyConfigurationResult>
{
    private protected BaseConfigureDeviceCommand( DeviceConfiguration configuration, (string lockedName, string? lockedControllerKey)? locked = null )
        : base( locked )
    {
        Throw.CheckNotNullArgument( configuration );
        ExternalConfiguration = configuration;
        ImmediateSending = true;
        ShouldCallDeviceOnCommandCompleted = false;
    }

    /// <summary>
    /// Transforms cancellation into <see cref="DeviceApplyConfigurationResult.ConfigurationCanceled"/> result.
    /// </summary>
    /// <param name="result">The result setter.</param>
    protected override sealed void OnCanceled( ref CompletionSource<DeviceApplyConfigurationResult>.OnCanceled result )
    {
        result.SetResult( DeviceApplyConfigurationResult.ConfigurationCanceled );
    }

    /// <summary>
    /// Transforms InvalidControllerKeyException into <see cref="DeviceApplyConfigurationResult.InvalidControllerKey"/>
    /// and any other error into <see cref="DeviceApplyConfigurationResult.UnexpectedError"/> result.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="result">The result setter.</param>
    protected override sealed void OnError( Exception ex, ref CompletionSource<DeviceApplyConfigurationResult>.OnError result )
    {
        if( ex is InvalidControllerKeyException ) result.SetResult( DeviceApplyConfigurationResult.InvalidControllerKey );
        else result.SetResult( DeviceApplyConfigurationResult.UnexpectedError );
    }

    /// <summary>
    /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the configuration can obviously be applied while the device is stopped.
    /// Note that this is not used: basic commands are always run by design.
    /// </summary>
    protected internal override sealed DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

    /// <summary>
    /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/>: the configuration can obviously be applied while the device is stopped.
    /// Note that this is not used: basic commands are always run by design.
    /// </summary>
    protected internal override sealed DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

    /// <summary>
    /// Gets the configuration to apply.
    /// <para>
    /// This MUST not be changed once the command is sent (<see cref="BaseDeviceCommand.IsLocked"/> is true)
    /// otherwise kitten will die.
    /// </para>
    /// </summary>
    public DeviceConfiguration ExternalConfiguration { get; }

}
