namespace CK.DeviceModel
{
    /// <summary>
    /// Immutable capture of a device and its current configuration.
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
        /// This is NOT the actual configuration that the device has received and on which it may keep
        /// a reference: configuration objects are cloned in order to isolate the running device
        /// of any change in this configuration.
        /// Changing this object is harmless.
        /// </summary>
        public TConfiguration Configuration { get; }

        internal ConfiguredDevice( T device, TConfiguration configuration )
        {
            Device = device;
            Configuration = configuration;
        }
    }

}
