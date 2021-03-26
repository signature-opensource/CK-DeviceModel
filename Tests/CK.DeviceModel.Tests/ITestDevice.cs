using CK.Core;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{
    public interface ITestDevice : IDevice
    {
        Task SendAutoDestroyAsync( IActivityMonitor monitor );
        Task<bool> SendForceAutoStopAsync( IActivityMonitor monitor );
    }
}
