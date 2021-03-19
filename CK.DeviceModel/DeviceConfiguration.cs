using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for configuration.
    /// </summary>
    public abstract class DeviceConfiguration : ICloneableCopyCtor
    {
        /// <summary>
        /// Initializes a new device configuration with an empty name and a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected DeviceConfiguration()
        {
            Name = String.Empty;
        }

        /// <summary>
        /// Copy constructor (see <see cref="ICloneableCopyCtor"/>).
        /// Specialized configurations MUST implement their copy constructor.
        /// </summary>
        /// <param name="source">The source configuration to copy.</param>
        protected DeviceConfiguration( DeviceConfiguration source )
        {
            Name = source.Name;
            Status = source.Status;
            ControllerKey = source.ControllerKey;
        }

        /// <summary>
        /// Gets or sets the name of the device.
        /// This is a unique key for a device in its host.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="DeviceConfigurationStatus"/>.
        /// </summary>
        public DeviceConfigurationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the configured controller key.
        /// When not null this locks the <see cref="IDevice.ControllerKey"/> to this value.
        /// When a configuration is applied, this configuration value overrides any existing device's controller key.
        /// </summary>
        public string? ControllerKey { get; set; }


        /// <summary>
        /// Gets or sets 
        /// </summary>
        public DeviceCommandStoppedBehavior DefaultStoppedBehavior { get; set; }

        /// <summary>
        /// Checks whether this configuration is valid.
        /// This checks that the <see cref="Name"/> is not empty and calls the protected <see cref="DoCheckValid(IActivityMonitor)"/>
        /// that can handle specialized checks.
        /// </summary>
        /// <param name="monitor">The monitor to log errors or warnings or information.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        public bool CheckValid( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( String.IsNullOrWhiteSpace( Name ) )
            {
                monitor.Error( "Configuration name must be a non empty string." );
                return false;
            }
            return DoCheckValid( monitor );
        }

        /// <summary>
        /// Optional extension point to check for validity.
        /// Always returns true by default.
        /// </summary>
        /// <param name="monitor">The monitor to log error, warnings or other.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        protected virtual bool DoCheckValid( IActivityMonitor monitor ) => true;

    }
}
