using CK.Core;
using System.Threading.Tasks;

namespace CK.DeviceModel;

interface IInternalDevice : IDevice
{
    DeviceConfigurationStatus ConfigStatus { get; }

    void OnCommandCompleted( BaseDeviceCommand cmd );

    Task EnsureInitialLifetimeEventAsync( IActivityMonitor monitor );
}
