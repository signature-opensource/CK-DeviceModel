using System;

namespace CK.DeviceModel
{
    /// <summary>
    /// Immutable capture of a device and its <see cref="IDevice.ExternalConfiguration"/>.
    /// </summary>
    public readonly struct ConfiguredDeviceSnapshot<T, TConfiguration>
        where T : Device<TConfiguration>
        where TConfiguration : DeviceConfiguration
    {
        /// <summary>
        /// Gets the device.
        /// </summary>
        public T Device { get; }

        /// <summary>
        /// Gets the <see cref="IDevice.ExternalConfiguration"/> at the time of the snapshot.
        /// </summary>
        public TConfiguration Configuration { get; }

        /// <summary>
        /// Initializes a new snapshot.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="configuration">The external configuration.</param>
        public ConfiguredDeviceSnapshot( T device, TConfiguration configuration )
        {
            Device = device ?? throw new ArgumentNullException( nameof( device ) );
            Configuration = configuration ?? throw new ArgumentNullException( nameof( configuration ) );
        }
    }

}
