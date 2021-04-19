using System.Threading.Tasks;
using CK.Core;

namespace CK.DeviceModel
{
    interface IInternalDevice
    {
        void Execute( IActivityMonitor monitor, SyncDeviceCommand c );

        Task ExecuteAsync( IActivityMonitor monitor, AsyncDeviceCommand c );
    }

}
