namespace CK.DeviceModel
{
    /// <summary>
    /// Describes the basic operations supported by a device.
    /// </summary>
    public enum BasicControlDeviceOperation
    {
        /// <summary>
        /// The <see cref="AsyncDeviceCommand.ControllerKey"/> of this command (that may be null) will be set as the controller key.
        /// This can fail if the <see cref="IDevice.ControllerKey"/> is fixed by the <see cref="DeviceConfiguration.ControllerKey"/>.
        /// </summary>
        ResetControllerKey,

        /// <summary>
        /// Attempts to start the device. 
        /// </summary>
        Start,

        /// <summary>
        /// Attempts to stop the device. 
        /// </summary>
        Stop
    }
}
