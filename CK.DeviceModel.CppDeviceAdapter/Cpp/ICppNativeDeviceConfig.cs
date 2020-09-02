namespace CK.DeviceModel.CppDeviceAdapter.Cpp
{
    /// <summary>
    /// Configuration of the native Cpp device.
    /// </summary>
    public interface ICppNativeDeviceConfig
    {
        /// <summary>
        /// Clones the current configuration and obtains a deep copy of the current configuration.
        /// </summary>
        /// <returns>A new, cloned configuration identical to the current configuration.</returns>
        ICppNativeDeviceConfig Clone();

    }
}
