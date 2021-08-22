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
        where TConfiguration : DeviceConfiguration
    {
        private protected BaseConfigureDeviceCommand( TConfiguration? configuration, (string lockedName, string? lockedControllerKey)? locked = null )
            : base( configuration ?? Activator.CreateInstance<TConfiguration>(), locked )
        {
            Debug.Assert( IsLocked == (locked != null) );
            if( IsLocked ) ClonedConfig = Configuration.DeepClone();
        }

        internal TConfiguration? ClonedConfig { get; private set; }

        /// <summary>
        /// Gets the configuration to apply.
        /// </summary>
        public new TConfiguration Configuration => (TConfiguration)base.Configuration;

        /// <summary>
        /// Overridden to snapshot the Configuration object into an internal property.
        /// </summary>
        public override void Lock()
        {
            if( !IsLocked )
            {
                ClonedConfig = Configuration.DeepClone();
                base.Lock();
            }
        }

    }
}
