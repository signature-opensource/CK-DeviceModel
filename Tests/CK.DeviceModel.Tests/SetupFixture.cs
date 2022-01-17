using CK.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{
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
            using var restoreFilter = Util.CreateDisposableAction( () => ActivityMonitor.Tags.RemoveFilter( IDeviceHost.DeviceModel ) );
        }
    }
}
