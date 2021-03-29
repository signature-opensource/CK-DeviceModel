namespace CK.DeviceModel
{
    /// <summary>
    /// Immutable capture of a device, its current configuration and application result.
    /// The configuration is not the one of the device: see <see cref="Configuration"/>.
    /// </summary>
    public readonly struct ConfiguredDevice<T, TConfiguration>
        where T : Device<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Gets the device.
        /// </summary>
        public T Device { get; }

        /// <summary>
        /// Gets the configuration that has been applied.
        /// This is NOT the actual configuration object reference that the device has received and
        /// on which it may keep a reference: configuration objects are cloned in order to isolate
        /// the running device of any change in this configuration.
        /// <para>
        /// Changing this object is harmless, it does nothing.
        /// </para>
        /// </summary>
        public TConfiguration Configuration { get; }

        /// <summary>
        /// Gets the result of the device configuration.
        /// </summary>
        public DeviceApplyConfigurationResult ConfigurationResult { get; }

        internal ConfiguredDevice( T device, TConfiguration configuration, DeviceApplyConfigurationResult result )
        {
            Device = device;
            Configuration = configuration;
            ConfigurationResult = result;
        }
    }

}
