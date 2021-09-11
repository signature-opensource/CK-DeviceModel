using CK.Core;
using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// Base class for configuration.
    /// <para>
    /// Concrete DeviceConfiguration classes should be sealed since simple binary de/serialization
    /// and auto configuration don't support polymorphism.
    /// </para>
    /// </summary>
    public abstract class DeviceConfiguration : ICKSimpleBinarySerializable
    {
        string _name;

        /// <summary>
        /// Initializes a new device configuration with an empty name and a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected DeviceConfiguration()
        {
            _name = String.Empty;
            BaseImmediateCommandLimit = 10;
        }

        /// <summary>
        /// Deserialization constructor.
        /// Every specialized configuration MUST define its own deserialization
        /// constructor (that must call its base) and the <see cref="Write(ICKBinaryWriter)"/>
        /// method must be overridden.
        /// </summary>
        /// <param name="r">The reader.</param>
        protected DeviceConfiguration( ICKBinaryReader r )
        {
            r.ReadByte(); // Version
            _name = r.ReadString();
            Status = r.ReadEnum<DeviceConfigurationStatus>();
            ControllerKey = r.ReadNullableString();
            BaseImmediateCommandLimit = r.ReadInt32();
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
            w.Write( BaseImmediateCommandLimit );
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
        /// Gets or sets the maximum number of immediate commands that will be handled
        /// before allowing one "normal" command to be handled.
        /// Defaults to 10. Must be between 1 and 1000 (included).
        /// <para>
        /// The actual limit is the sum of this base and the <see cref="IDevice.ImmediateCommandLimitOffset"/>
        /// and will always be between 1 and 1000.
        /// </para>
        /// <para>
        /// When a configuration is applied, this BaseImmediateCommandLimit always takes effect:
        /// <list type="bullet">
        ///     <item>
        ///     This configuration is orthogonal to any other ones.
        ///     </item>
        ///     <item>
        ///     It's an advanced configuration that may be used "dynamically" to
        ///     fix a starvation issue in a running system: whether the actual device's
        ///     reconfiguration succeeds or not is not relevant to this change.
        ///     </item>
        /// </list>
        /// </para>
        /// </summary>
        public int BaseImmediateCommandLimit { get; set; }

        /// <summary>
        /// Checks whether this configuration is valid.
        /// This checks that the <see cref="Name"/> is not empty and always calls the protected <see cref="DoCheckValid(IActivityMonitor)"/>
        /// that can handle specialized checks.
        /// </summary>
        /// <param name="monitor">The monitor to log errors or warnings or information.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        public bool CheckValid( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            bool success = true;
            if( String.IsNullOrWhiteSpace( Name ) )
            {
                monitor.Error( "Configuration name must be a non empty string." );
                success = false;
            }
            if( BaseImmediateCommandLimit <= 0 || BaseImmediateCommandLimit > 1000 )
            {
                monitor.Error( "BaseImmediateCommandLimit must be between 1 and 1000." );
                success = false;
            }
            return DoCheckValid( monitor ) && success;
        }

        /// <summary>
        /// Optional extension point to check for validity that is aways called by CheckValid (even if
        /// <see cref="Name"/> or <see cref="BaseImmediateCommandLimit"/> are invalid) so that the whole
        /// configuration can be checked.
        /// <para>
        /// Always returns true by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to log error, warnings or other.</param>
        /// <returns>True if this configuration is valid, false otherwise.</returns>
        protected virtual bool DoCheckValid( IActivityMonitor monitor ) => true;

    }
}
