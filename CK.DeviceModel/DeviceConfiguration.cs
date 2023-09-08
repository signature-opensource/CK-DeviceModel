using CK.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;

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
        /// Initializes a new device configuration with an automatic (awful) unique name and
        /// a <see cref="DeviceConfigurationStatus.Disabled"/> status.
        /// </summary>
        protected DeviceConfiguration()
        {
            _name = Util.GetRandomBase64UrlString( 11 );
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
            Status = (DeviceConfigurationStatus)r.ReadInt32();
            ControllerKey = r.ReadNullableString();
            BaseImmediateCommandLimit = r.ReadInt32();
        }

        /// <summary>
        /// Writes this configuration to the binary writer.
        /// This method MUST be overridden and MUST start with:
        /// <list type="bullet">
        ///     <item>A call to <c>base.Write( w );</c></item>
        ///     <item>Writing its version number (typically a byte).</item>
        /// </list>
        /// </summary>
        /// <param name="w">The writer.</param>
        public virtual void Write( ICKBinaryWriter w )
        {
            w.Write( (byte)0 ); // Version
            w.Write( _name );
            w.Write( (int)Status );
            w.WriteNullableString( ControllerKey );
            w.Write( BaseImmediateCommandLimit );
        }

        /// <summary>
        /// Gets or sets the name of the device.
        /// This is a unique key for a device in its host.
        /// <para>
        /// Defaults to a random string (see <see cref="Util.GetRandomBase64UrlString(int)"/>).
        /// </para>
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                Throw.CheckNotNullArgument( value );
                _name = value;
            }
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
            Throw.CheckNotNullArgument( monitor );
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


        /// <summary>
        /// Creates a configuration object from a JSON string configuration.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="deviceConfigurationType">The device configuration type.</param>
        /// <param name="jsonConfiguration">The device configuration in JSON format.</param>
        /// <returns>The configuration or null on error (if the configuration cannot be created).</returns>
        public static DeviceConfiguration? CreateFromJson( IActivityMonitor monitor, Type deviceConfigurationType, string deviceName, string jsonConfiguration )
        {
            var c = new MutableConfigurationSection( deviceName );
            c.AddJson( jsonConfiguration );
            return CreateFromConfiguration( monitor, deviceConfigurationType, c );
        }


        /// <summary>
        /// Creates a configuration object from a <see cref="IConfigurationSection"/>.
        /// The <see cref="IConfigurationSection.Key"/> is the device name.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="deviceConfigurationType">The device configuration type.</param>
        /// <param name="c">The configuration section.</param>
        /// <returns>The configuration or null on error (if the configuration cannot be created).</returns>
        public static DeviceConfiguration? CreateFromConfiguration( IActivityMonitor monitor, Type deviceConfigurationType, IConfigurationSection c )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( deviceConfigurationType );
            Throw.CheckNotNullArgument( c );
            Throw.CheckArgument( typeof( DeviceConfiguration ).IsAssignableFrom( deviceConfigurationType ) );

            if( !DeviceHostDaemon.FindSpecificConstructors( monitor,
                                                            deviceConfigurationType,
                                                            out ConstructorInfo? ctor0,
                                                            out ConstructorInfo? ctor1,
                                                            out ConstructorInfo? ctor2 ) )
            {
                // No constructor found. This is a serious error.
                return null;
            }
            DeviceConfiguration? configObject = null;
            try
            {
                if( ctor2 != null )
                {
                    configObject = (DeviceConfiguration?)ctor2.Invoke( new object[] { monitor, c } );
                }
                else if( ctor1 != null )
                {
                    configObject = (DeviceConfiguration?)ctor1.Invoke( new object[] { c } );
                }
                else
                {
                    configObject = (DeviceConfiguration?)c.Get( deviceConfigurationType );
                }
            }
            catch( Exception ex )
            {
                monitor.Error( $"While instantiating Device configuration for '{c.Path}' and type '{deviceConfigurationType:C}'.", ex );
            }
            if( configObject == null )
            {
                monitor.Warn( $"Unable to bind configuration entry '{c.Key}'." );
            }
            else
            {
                configObject.Name = c.Key;
            }
            return configObject;
        }

    }
}
