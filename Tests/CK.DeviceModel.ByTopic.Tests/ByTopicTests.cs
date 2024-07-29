using CK.Core;
using CK.Cris;
using CK.DeviceModel.ByTopic.CommandHandlers;
using CK.DeviceModel.ByTopic.Commands;
using CK.DeviceModel.ByTopic.IO;
using CK.DeviceModel.ByTopic.Tests.Helpers;
using CK.DeviceModel.ByTopic.Tests.Hosts;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using static CK.Testing.StObjSetupTestHelper;

namespace CK.DeviceModel.ByTopic.Tests
{
    public class Tests
    {
        [AllowNull]
        AutomaticServices _auto;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisBackgroundExecutorService ),
                                                  typeof( CrisBackgroundExecutor ),
                                                  typeof( CrisExecutionContext ),
                                                  typeof( FakeLEDStripHosts ),
                                                  typeof( FakeSignatureDeviceHosts ),
                                                  typeof( ITurnOffLocationCommand ),
                                                  typeof( ByTopicCommandHandler ),
                                                  typeof( Validators)
                                                  );
            _auto = configuration.RunSuccessfully().CreateAutomaticServices();
        }

        [OneTimeTearDown]
        public void OneTimeDearDown()
        {
            _auto.Dispose();
        }

        [Test]
        public async Task vali()
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOffCmd = pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                {
                    r.Topic = "Test";
                } );
                await CrisHelper.SendCrisCommandAsync( turnOffCmd, TestHelper.Monitor, cbe );
            }

        }
    }
}
