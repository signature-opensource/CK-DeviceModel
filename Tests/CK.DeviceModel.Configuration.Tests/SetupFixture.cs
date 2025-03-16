using CK.Core;
using NUnit.Framework;

namespace CK.DeviceModel;

[SetUpFixture]
public class MySetUpClass
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        ActivityMonitor.Tags.AddFilter( IDeviceHost.DeviceModel, new LogClamper( LogFilter.Debug, true ) );
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        ActivityMonitor.Tags.RemoveFilter( IDeviceHost.DeviceModel );
    }
}
