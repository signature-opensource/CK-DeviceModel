using CK.Core;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{
    public interface ITestDevice : IDevice
    {
        Task TestAutoDestroyAsync( IActivityMonitor monitor );
        Task TestForceStopAsync( IActivityMonitor monitor );
    }
}
