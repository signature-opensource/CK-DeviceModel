using CK.Core;
using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for configuration.
    /// </summary>
    public abstract class DeviceConfiguration : ICloneableCopyCtor
    {
        string _name;

        /// <summary>
        /// Initializes a new device configuration with an empty name and a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected DeviceConfiguration()
        {
            _name = String.Empty;
        }

        /// <summary>
        /// Copy constructor (see <see cref="ICloneableCopyCtor"/>).
        /// Specialized configurations MUST implement their copy constructor.
        /// </summary>
        /// <param name="source">The source configuration to copy.</param>
        protected DeviceConfiguration( DeviceConfiguration source )
        {
            _name = source.Name;
            Status = source.Status;
            ControllerKey = source.ControllerKey;
        }

        /// <summary>
        /// Deserialization constructor.
        /// Every specialized configuration MUST define its own deserialization
        /// constructor (that must call its base) and override the <see cref="Write(ICKBinaryWriter)"/>
        /// method (that must start to call its base Write method).
        /// </summary>
        /// <param name="r">The reader.</param>
        protected DeviceConfiguration( ICKBinaryReader r )
        {
            r.ReadByte(); // Version
            _name = r.ReadString();
            Status = r.ReadEnum<DeviceConfigurationStatus>();
            ControllerKey = r.ReadNullableString();
        }

        /// <summary>
        /// Writes this configuration to the binary writer.
        /// This method MUST be overridden and MUST start with:
        /// <list type="bullet">
        ///     <item>A call to <c>base.Write( w );</item>
        ///     <item>Writing its version number (typically a byte).</item>
        /// </list>
        /// </summary>
        /// <param name="w">The writer.</param>
        public virtual void Write( ICKBinaryWriter w )
        {
            w.Write( (byte)0 ); // Version
            w.Write( _name );
            w.WriteEnum( Status );
            w.WriteNullableString( ControllerKey );
        }


        /// <summary>
        /// Gets or sets the name of the device.
        /// This is a unique key for a device in its host.
        /// </summary>
        public string Name
        {
            get => _name;
            set => _name = value ?? throw new ArgumentNullException( nameof( Name ) );
        }

        /// <summary>
        /// Gets or sets the <see cref="DeviceConfigurationStatus"/>.
        /// Defaults to <see cref="DeviceConfigurationStatus.Disabled"/>.
        /// </summary>
        public DeviceConfigurationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the configured controller key.
        /// When not null this locks the <see cref="IDevice.ControllerKey"/> to this value.
        /// When a configuration is applied, this configuration value overrides any existing device's controller key.
        /// </summary>
        public string? ControllerKey { get; set; }

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
