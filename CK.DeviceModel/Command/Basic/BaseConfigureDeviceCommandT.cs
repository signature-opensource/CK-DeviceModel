using CK.Core;
using System;
using System.Diagnostics;

namespace CK.DeviceModel
{
    /// <summary>
    /// This class cannot be directly specialized: the generic <see cref="ConfigureDeviceCommand{THost,TConfiguration}"/>
    /// must be used.
    /// </summary>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract class BaseConfigureDeviceCommand<TConfiguration> : BaseConfigureDeviceCommand
        where TConfiguration : DeviceConfiguration, new()
    {
        private protected BaseConfigureDeviceCommand( TConfiguration? externalConfiguration, TConfiguration? clonedConfiguration, (string lockedName, string? lockedControllerKey)? locked = null )
            : base( externalConfiguration ?? Activator.CreateInstance<TConfiguration>(), locked )
        {
            Debug.Assert( IsLocked == (locked != null) );
            Debug.Assert( !IsLocked || (clonedConfiguration != null), "IsLocked => configuration provided." );
            ClonedConfig = clonedConfiguration;
        }

        internal TConfiguration? ClonedConfig { get; private set; }

        /// <summary>
        /// Gets the configuration to apply.
        /// </summary>
        public new TConfiguration ExternalConfiguration => (TConfiguration)base.ExternalConfiguration;

        /// <summary>
        /// Calls <see cref="DeviceConfiguration.CheckValid(IActivityMonitor)"/> on the <see cref="ExternalConfiguration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if the <see cref="ExternalConfiguration"/> is valid, false otherwise.</returns>
        protected override sealed bool DoCheckValidity( IActivityMonitor monitor )
        {
            // When the command is locked, the command must have been validated...
            if( IsLocked ) return true;
            if( !ExternalConfiguration.CheckValid( monitor ) ) return false;
            ClonedConfig = ExternalConfiguration.DeepClone();
            if( !ClonedConfig.CheckValid( monitor ) )
            {
                Throw.CKException( "Cloned configuration CheckValid failed but the external has been validated. Something really weird happens." );
            }
            return true;
        }


    }
}
