using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non generic base command class that exposes the host that must handle it.
    /// This class cannot be directly specialized: the generic <see cref="DeviceCommand{THost}"/>
    /// must be used, or the <see cref="DeviceCommand{THost,TResult}"/> when the command generates
    /// a result.
    /// </summary>
    public abstract class BaseDeviceCommand
    {
        string _deviceName;
        string? _controllerKey;
        // Internal for the command queue.
        internal DateTime SendTime;
        CancellationTokenRegistration[] _cancels;
        static readonly Action<object?> _cancelFromTokenHandle = o => ((BaseDeviceCommand)o!).CancelFromCancellationTokens();
        readonly CancellationTokenSource _cancelsResult;
        bool _isLocked;

        // Running data...
        // ...initialized by OnCommandEnter.
        internal ActivityMonitor.DependentToken? _dependentToken;
        // ...initialized by OnCommandSend.
        internal bool _mustCheckControllerKey;
        IInternalDevice? _device;

        /// <summary>
        /// Initialize a new locked command if <paramref name="locked"/> is provided.
        /// Otherwise initializes a new unlocked command (DeviceName is empty, ControllerKey is null).
        /// </summary>
        /// <param name="locked">The device name and controller key or null.</param>
        private protected BaseDeviceCommand( (string lockedName, string? lockedControllerKey)? locked = null )
        {
            if( locked.HasValue )
            {
                (_deviceName, _controllerKey) = locked.Value;
                _isLocked = true;
            }
            else
            {
                _deviceName = String.Empty;
            }
            ShouldCallDeviceOnCommandCompleted = true;
            SendTime = Util.UtcMinValue;
            _cancels = Array.Empty<CancellationTokenRegistration>();
            _cancelsResult = new CancellationTokenSource();
        }

        /// <summary>
        /// Gets the type of the host for the command.
        /// </summary>
        public abstract Type HostType { get; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel"/> since most of the commands
        /// should not be executed while the device is stopped and this enables always running devices to be resilient to
        /// unattended stops (and subsequent restarts).
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// When <see cref="ImmediateSending"/> is true, <see cref="ImmediateStoppedBehavior"/> applies and this property is ignored.
        /// </para>
        /// </summary>
        protected internal virtual DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.WaitForNextStartWhenAlwaysRunningOrCancel;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.Cancel"/> since most of the commands
        /// should not be executed while the device is stopped.
        /// <para>
        /// Some commands may override this, or the device can alter this behavior thanks to its
        /// <see cref="Device{TConfiguration}.OnStoppedDeviceImmediateCommand(IActivityMonitor, BaseDeviceCommand)"/> protected method.
        /// </para>
        /// <para>
        /// This applies when <see cref="ImmediateSending"/> is true. See <see cref="StoppedBehavior"/> otherwise.
        /// </para>
        /// </summary>
        protected internal virtual DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.Cancel;

        /// <summary>
        /// Gets or sets whether this command must be sent and handled immediately.
        /// Defaults to false, except for the 5 basic commands (Start, Stop, Configure, SetControllerKey and Destroy).
        /// </summary>
        public bool ImmediateSending
        {
            get => SendTime.Kind == DateTimeKind.Unspecified;
            set
            {
                // We don't check _isLocked here for 2 reasons:
                // 1 - This is called to configure the already locked basic command default configuration.
                // 2 - this is pointless since Lock() is called after the routing between Immediate and regular
                //     command queues has been made.
                // The side effect is that if Lock() is called by user code before sending the command, this can
                // still be changed. But we don't care :).
                SendTime = value ? DateTime.MinValue : Util.UtcMinValue;
            }
        }

        /// <summary>
        /// Gets or sets the sending time of this command.
        /// When null (the default) or set to <see cref="Util.UtcMinValue"/> the command is executed
        /// (as usual) when it is dequeued.
        /// <para>
        /// When <see cref="ImmediateSending"/> is set to true, this SendingTimeUtc is automatically set to null.
        /// And when this is set to a non null UTC time, the ImmediateSending is automatically set to false.
        /// </para>
        /// <para>
        /// The value should be in the future but no check is done against <see cref="DateTime.UtcNow"/>
        /// in order to safely handle any clock drift: if the time is in the past when the command is dequeued,
        /// it will be executed like any regular (non immediate) command.
        /// </para>
        /// </summary>
        public DateTime? SendingTimeUtc
        {
            get => SendTime.Ticks == 0 ? null : SendTime;
            set
            {
                ThrowOnLocked();
                if( !value.HasValue )
                {
                    SendTime = Util.UtcMinValue;
                }
                else
                {
                    Throw.CheckArgument( value.Value.Kind == DateTimeKind.Utc );
                    SendTime = value.Value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether <see cref="Device{TConfiguration}.OnCommandCompletedAsync(IActivityMonitor, BaseDeviceCommand)"/>
        /// should be called once the command completed.
        /// Defaults to true, except for the 5 basic commands (Start, Stop, Configure, SetControllerKey and Destroy).
        /// </summary>
        public bool ShouldCallDeviceOnCommandCompleted { get; set; }

        /// <summary>
        /// Gets or sets the target device name.
        /// <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/> requires this name to
        /// be the one of the device (see <see cref="IDevice.Name"/>) otherwise the command is ignored.
        /// <para>
        /// Note that when this command is sent to the device, this name must not be null nor empty (and, more generally,
        /// <see cref="CheckValidity(IActivityMonitor)"/> must return true).
        /// </para>
        /// </summary>
        public string DeviceName
        {
            get => _deviceName;
            set
            {
                Throw.CheckNotNullArgument( value );
                if( value != _deviceName )
                {
                    ThrowOnLocked();
                    _deviceName = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the required controller key. See <see cref="IDevice.ControllerKey"/>
        /// and <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, CancellationToken)"/>.
        /// <para>
        /// Note that if the target <see cref="IDevice.ControllerKey"/> is null, all commands are accepted.
        /// </para>
        /// </summary>
        public string? ControllerKey
        {
            get => _controllerKey;
            set
            {
                ThrowOnLocked();
                _controllerKey = value;
            }
        }

        /// <summary>
        /// Gets whether this command has been submitted and should not be altered anymore.
        /// </summary>
        public bool IsLocked => _isLocked;

        /// <summary>
        /// Checks the validity of this command. <see cref="DeviceName"/> must not be null.
        /// This calls the protected <see cref="DoCheckValidity(IActivityMonitor)"/> that should be overridden to
        /// check specific command parameters constraints.
        /// <para>
        /// This can be called even if <see cref="IsLocked"/> is true.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor that will be used to emit warnings or errors.</param>
        /// <returns>Whether this configuration is valid.</returns>
        public bool CheckValidity( IActivityMonitor monitor )
        {
            Debug.Assert( HostType != null, "Thanks to the private protected constructor and the generic of <THost>, a command instance has a host type." );
            if( DeviceName == null )
            {
                monitor.Error( $"Command '{ToString()}': DeviceName must not be null." );
                return false;
            }
            if( InternalCompletion.IsCompleted )
            {
                monitor.Error( $"{ToString()} has already a Result. Command cannot be reused." );
                return false;
            }
            if( _device != null )
            {
                monitor.Error( $"{ToString()} has already been sent to device '{_device.FullName}'. A command can only be sent once." );
                return false;
            }
            return DoCheckValidity( monitor );
        }

        /// <summary>
        /// Sets <see cref="IsLocked"/> to true.
        /// Called once the command is submitted (it has already been successfully validated).
        /// This method can be overridden to prepare the command (like cloning internal data).
        /// <para>
        /// Override should ensure that this method can safely be called multiple times.
        /// </para>
        /// </summary>
        public virtual void Lock()
        {
            _isLocked = true;
        }

        /// <summary>
        /// Helper method that raises an <see cref="InvalidOperationException"/> if <see cref="IsLocked"/> is true.
        /// </summary>
        protected void ThrowOnLocked()
        {
            if( _isLocked ) Throw.InvalidOperationException( $"Command '{ToString()}' is locked." );
        }

        /// <summary>
        /// Registers a source for this <see cref="CancellationToken"/>.
        /// Nothing is done if <see cref="CancellationToken.CanBeCanceled"/> is false
        /// or this command has already been completed (see <see cref="ICompletion.IsCompleted"/>).
        /// <para>
        /// Whenever one of the added token is canceled, <see cref="ICompletionSource.TrySetCanceled()"/> is called.
        /// If the token is already canceled, the call to try to cancel the completion is made immediately.
        /// </para>
        /// </summary>
        /// <param name="t">The token.</param>
        /// <returns>True if the token has been registered or triggered the cancellation, false otherwise.</returns>
        public bool AddCancellationSource( CancellationToken t )
        {
            if( t.CanBeCanceled && !InternalCompletion.IsCompleted )
            {
                if( t.IsCancellationRequested )
                {
                    CancelFromCancellationTokens();
                }
                else
                {
                    // Register returns a dummy registration if the token has been signaled.
                    var c = t.UnsafeRegister( _cancelFromTokenHandle, this );
                    // Instead of using CreateLinkedTokenSource and its linked list
                    // for which we'll have to handle the head with an interlocked setter anyway,
                    // we use a reallocated array of registrations because:
                    //  - it results in much less allocations (one array of value type instead of the linked nodes).
                    //  - concurrency should be exceptional (interlocked operations will almost always occur once).
                    Util.InterlockedAdd( ref _cancels, c );
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a cancellation token that combines all tokens added by <see cref="AddCancellationSource(CancellationToken)"/>.
        /// It is signaled as soon as one of the source token is signaled but not when the Completion is canceled: it
        /// must be used to cancel any operation related to the command execution. 
        /// </summary>
        public CancellationToken CancellationToken => _cancelsResult.Token;

        void CancelFromCancellationTokens()
        {
            _cancelsResult.Cancel();
            InternalCompletion.TrySetCanceled();
        }

        /// <summary>
        /// Because of covariant return type limitation, this property unifies the <see cref="DeviceCommandNoResult.Completion"/>
        /// and <see cref="DeviceCommandWithResult{TResult}.Completion"/>.
        /// </summary>
        internal abstract ICompletionSource InternalCompletion { get; }

        internal void OnCommandEnter( ActivityMonitor.DependentToken d )
        {
            _dependentToken = d;
        }

        internal void OnCommandSend( IInternalDevice device, bool checkControllerKey, CancellationToken token )
        {
            if( _device != null ) Throw.InvalidOperationException( $"Command '{ToString()}' has already been sent." );
            Lock();
            _device = device;
            _mustCheckControllerKey = checkControllerKey;
            if( token.CanBeCanceled ) AddCancellationSource( token );
        }

        // This is called by the ICompletable.OnCompleted implementations of DeviceCommandNoResult
        // and DeviceCommandWithResult.
        private protected void OnInternalCommandCompleted()
        {
            Debug.Assert( InternalCompletion.IsCompleted );
            Debug.Assert( _device != null );
            Util.InterlockedSet( ref _cancels, cancels =>
            {
                foreach( var c in cancels ) c.Dispose();
                return Array.Empty<CancellationTokenRegistration>();
            } );
            if( ShouldCallDeviceOnCommandCompleted )
            {
                _device.OnCommandCompleted( this );
            }
        }

        /// <summary>
        /// Extension point to <see cref="CheckValidity(IActivityMonitor)"/>. Called only after basic checks successfully passed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True for a valid configuration, false otherwise.</returns>
        protected virtual bool DoCheckValidity( IActivityMonitor monitor ) => true;

    }
}
