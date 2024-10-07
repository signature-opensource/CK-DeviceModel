using CK.Core;
using System;
using System.Diagnostics;

namespace CK.DeviceModel;

/// <summary>
/// Base device commands that generates a result.
/// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost,TResult}"/> must be used.
/// </summary>
/// <typeparam name="TResult">The type of the command's result.</typeparam>
public abstract class DeviceCommandWithResult<TResult> : BaseDeviceCommand, ICompletable<TResult>
{
    readonly string _commandToString;

    /// <inheritdoc />
    private protected DeviceCommandWithResult( (string lockedName, string? lockedControllerKey)? locked = null )
        : base( locked )
    {
        Completion = new CompletionSource<TResult>( this );
        _commandToString = GetType().ToCSharpName( withNamespace: false );
    }

    /// <summary>
    /// Gets the <see cref="CompletionSource"/> for this command.
    /// </summary>
    public CompletionSource<TResult> Completion { get; }

    /// <summary>
    /// Gets the <see cref="CompletionSource"/> for this command.
    /// </summary>
    ICompletion<TResult> ICompletable<TResult>.Completion => Completion;

    internal override ICompletionSource InternalCompletion => Completion;

    void ICompletable<TResult>.OnError( Exception ex, ref CompletionSource<TResult>.OnError result ) => OnError( ex, ref result );

    void ICompletable<TResult>.OnCanceled( ref CompletionSource<TResult>.OnCanceled result ) => OnCanceled( ref result );

    void ICompletable<TResult>.OnCompleted() => OnInternalCommandCompleted();

    /// <summary>
    /// Called by the CompletionSource when a error is set.
    /// This default implementation calls <see cref="CompletionSource{TResult}.OnError.SetException(Exception)"/>.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="result">Captures the result: one of the 3 available methods must be called.</param>
    protected virtual void OnError( Exception ex, ref CompletionSource<TResult>.OnError result ) => result.SetException( ex );

    /// <summary>
    /// Called by the CompletionSource when a cancellation occurred.
    /// This default implementation calls <see cref="CompletionSource{TResult}.OnCanceled.SetCanceled()"/>.
    /// </summary>
    /// <param name="result">Captures the result: one of the 2 available methods must be called.</param>
    protected virtual void OnCanceled( ref CompletionSource<TResult>.OnCanceled result ) => result.SetCanceled();

    /// <summary>
    /// Overridden to return this type name, <see cref="Completion"/> status and <see cref="BaseDeviceCommand.CancellationReason"/>.
    /// This cannot be overridden to secure how a command appears in the logs. You may use the protected <see cref="ToStringSuffix"/>
    /// property to append more details if required.
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
