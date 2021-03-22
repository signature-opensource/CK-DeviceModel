using CK.Core;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{
    public interface ITestDevice : IDevice
    {
        CommandCompletion SendAutoDestroy( IActivityMonitor monitor );
        CommandCompletion<bool> SendForceAutoStop( IActivityMonitor monitor );
    }
}
