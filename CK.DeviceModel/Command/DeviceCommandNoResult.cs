using CK.Core;
using System;

namespace CK.DeviceModel;

/// <summary>
/// Base command that doesn't return a result: its <see cref="Completion"/> can be awaited either
/// for completion or for error.
/// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost}"/>
/// must be used.
/// </summary>
public abstract class DeviceCommandNoResult : BaseDeviceCommand, ICompletable
{
    // Name is not factorized in BaseDeviceCommand because of DeviceCommandWithResult
    // that computes a clean name with the <TResult> and it wouldn't be readonly.
    readonly string _commandToString;

    /// <inheritdoc />
    private protected DeviceCommandNoResult( (string lockedName, string? lockedControllerKey)? locked = null )
        : base( locked )
    {
        Completion = new CompletionSource( this );
        _commandToString = GetType().Name;
    }

    /// <summary>
    /// Gets the <see cref="CompletionSource"/> for this command.
    /// </summary>
    public CompletionSource Completion { get; }

    ICompletion ICompletable.Completion => Completion;

    internal override ICompletionSource InternalCompletion => Completion;

    void ICompletable.OnError( Exception ex, ref CompletionSource.OnError result ) => OnError( ex, ref result );

    void ICompletable.OnCanceled( ref CompletionSource.OnCanceled result ) => OnCanceled( ref result );

    void ICompletable.OnCompleted() => OnInternalCommandCompleted();

    /// <summary>
    /// Called by the CompletionSource when a error is set.
    /// This default implementation calls <see cref="CompletionSource.OnError.SetException(Exception)"/>.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
    protected virtual void OnError( Exception ex, ref CompletionSource.OnError result ) => result.SetException( ex );

    /// <summary>
    /// Called by the CompletionSource when a cancellation occurred.
    /// This default implementation calls <see cref="CompletionSource.OnCanceled.SetCanceled()"/>.
    /// </summary>
    /// <param name="result">Captures the result: one of the 2 available methods must be called.</param>
    protected virtual void OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetCanceled();

    /// <summary>
    /// Overridden to return this type name, <see cref="Completion"/> status and <see cref="BaseDeviceCommand.CancellationReason"/>.
    /// This cannot be overridden to secure how a command appears in the logs. You may use the protected <see cref="ToStringSuffix"/>
    /// virtual property to append more details if required.
    /// </summary>
    /// <returns>This type name and current completion status.</returns>
    public override sealed string ToString() => CancellationReason == null
                                                ? $"{_commandToString}[{Completion}]{ToStringSuffix}"
                                                : $"{_commandToString}[{Completion}: {CancellationReason}]{ToStringSuffix}";

    /// <summary>
    /// Optional string that is appended to command <see cref="ToString()"/> (that cannot be overridden).
    /// It will appear after the type name and completion status.
    /// </summary>
    protected virtual string? ToStringSuffix => null;
}
