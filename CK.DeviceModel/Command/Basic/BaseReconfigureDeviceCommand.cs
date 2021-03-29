using CK.Core;
using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// This class cannot be directly specialized: the generic <see cref="ReconfigureDeviceCommand{THost,TConfiguration}"/>
    /// must be used.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract class BaseReconfigureDeviceCommand<TConfiguration> : DeviceCommandWithResult<DeviceApplyConfigurationResult>
        where TConfiguration : DeviceConfiguration
    {
        private protected BaseReconfigureDeviceCommand()
            : base( OnError, true, DeviceApplyConfigurationResult.ConfigurationCanceled )
        {
        }

        static DeviceApplyConfigurationResult OnError( Exception ex ) => ex switch
        {
            InvalidControllerKeyException => DeviceApplyConfigurationResult.InvalidControllerKey,
            _ => DeviceApplyConfigurationResult.UnexpectedError
        };

        internal TConfiguration? ExternalConfig { get; set; }

        /// <summary>
        /// Returns <see cref="DeviceCommandStoppedBehavior.RunAnyway"/>: the configuration can obviously be executed while the device is stopped.
        /// </summary>
        protected internal override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;

        /// <summary>
        /// Gets or sets the configuration to apply.
        /// </summary>
        public TConfiguration? Configuration { get; set; }

        /// <summary>
        /// Checks that the configuration is present and that <see cref="DeviceConfiguration.CheckValid(IActivityMonitor)"/> returns true.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        protected override bool DoCheckValidity( IActivityMonitor monitor )
        {
            if( Configuration == null )
            {
                monitor.Error( "Missing Configuration object." );
                return false;
            }
            return Configuration.CheckValid( monitor );
        }
    }
}
