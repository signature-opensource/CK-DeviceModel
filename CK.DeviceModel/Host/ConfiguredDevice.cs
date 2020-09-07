namespace CK.DeviceModel
{
    /// <summary>
    /// Immutable capture of a device and its current configuration.
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
        /// Gets the configuration.
        /// </summary>
        public TConfiguration Configuration { get; }

        internal ConfiguredDevice( T device, TConfiguration configuration )
        {
            Device = device;
            Configuration = configuration;
        }
    }

}
