using CK.Core;
using System;
using System.Diagnostics;

namespace CK.DeviceModel
{
    /// <summary>
    /// Non-generic base for the generic <see cref="ConfigureDeviceCommand{THost,TConfiguration}"/> (that
    /// is the only one that can be used).
    /// It exposes a base <see cref="DeviceConfiguration"/> and is useful when configuration type
    /// is not statically available.
    /// </summary>
    public abstract class BaseConfigureDeviceCommand : DeviceCommandWithResult<DeviceApplyConfigurationResult>, ICompletable<DeviceApplyConfigurationResult>
    {
        private protected BaseConfigureDeviceCommand( DeviceConfiguration configuration, (string lockedName, string? lockedControllerKey)? locked = null )
            : base( locked )
        {
            Throw.CheckNotNullArgument( configuration );
            Configuration = configuration;
            ImmediateSending = true;
        }

        void ICompletable<DeviceApplyConfigurationResult>.OnError( Exception ex, ref CompletionSource<DeviceApplyConfigurationResult>.OnError result )
        {
            if( ex is InvalidControllerKeyException ) result.SetResult( DeviceApplyConfigurationResult.InvalidControllerKey );
            else result.SetResult( DeviceApplyConfigurationResult.UnexpectedError );
        }

        void ICompletable<DeviceApplyConfigurationResult>.OnCanceled( ref CompletionSource<DeviceApplyConfigurationResult>.OnCanceled result )
        {
            result.SetResult( DeviceApplyConfigurationResult.ConfigurationCanceled );
        }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the configuration can obviously be applied while the device is stopped.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Returns <see cref="DeviceImmediateCommandStoppedBehavior.RunAnyway"/>: the configuration can obviously be applied while the device is stopped.
        /// Note that this is not used: basic commands are always run by design.
        /// </summary>
        protected internal override DeviceImmediateCommandStoppedBehavior ImmediateStoppedBehavior => DeviceImmediateCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Gets the configuration to apply.
        /// </summary>
        public DeviceConfiguration Configuration { get; }

        /// <summary>
        /// Calls <see cref="DeviceConfiguration.CheckValid(IActivityMonitor)"/> on the <see cref="Configuration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        protected override bool DoCheckValidity( IActivityMonitor monitor )
        {
            return Configuration.CheckValid( monitor );
        }

    }
}
