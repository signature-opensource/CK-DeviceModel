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
using NUnit.Framework.Legacy;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.StObjSetupTestHelper;

namespace CK.DeviceModel.ByTopic.Tests
{
    public class ByTopicTests
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
                                                  typeof( ISwitchLocationCommandResult ),
                                                  typeof( ISwitchMultipleLocationsCommandResult ),
                                                  typeof( ITurnOffLocationCommand ),
                                                  typeof( ITurnOnLocationCommand ),
                                                  typeof( ITurnOffMultipleLocationsCommand ),
                                                  typeof( ITurnOnMultipleLocationsCommand ),
                                                  typeof( ByTopicCommandHandler ),
                                                  typeof( Validators )
                                                  );
            _auto = configuration.RunSuccessfully().CreateAutomaticServices();
        }

        [OneTimeTearDown]
        public void OneTimeDearDown()
        {
            _auto.Dispose();
        }

        [Test]
        public async Task validators_should_remove_first_character_when_topic_begining_with_slash_async()
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();


                var topic = "/Test";
                var turnOffCmd = pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                {
                    r.Topic = topic;
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOffLocationCommand, ISwitchLocationCommandResult>( turnOffCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.ResultByDeviceName );

                Assert.That( result.Topic, Is.EqualTo( topic.Substring( 1 ) ) );

                foreach( var keyValuePair in result.ResultByDeviceName )
                {
                    ClassicAssert.True( keyValuePair.Value );
                }
            }

        }

        [Test]
        public async Task validators_should_collect_error_when_device_full_name_not_matching_async()
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOffCmd = pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                {
                    r.Topic = "Test";
                    r.DeviceFullName = "?";
                } );
                var ex = Assert.ThrowsAsync<CKException>( async () => await CrisHelper.SendCrisCommandAsync( turnOffCmd, TestHelper.Monitor, cbe ) );
                Assert.That( ex.Message, Is.EqualTo( "Command failed with 1 messages: Invalid DeviceFullName." ) );
            }

        }

        [TestCaseSource( nameof( SwitchTestsCases ) )]
        public async Task can_turn_on_location_async( (string topic, List<string> deviceThatShouldBeFalse) tc )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOnCmd = pocoDirectory.Create<ITurnOnLocationCommand>( r =>
                {
                    r.Topic = tc.topic;
                    r.Colors.Add(ColorLocation.Red);
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOnLocationCommand, ISwitchLocationCommandResult>( turnOnCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.ResultByDeviceName );

                foreach( var keyValuePair in result.ResultByDeviceName )
                {
                    if( tc.deviceThatShouldBeFalse.Contains( keyValuePair.Key ) )
                    {
                        ClassicAssert.False( keyValuePair.Value );
                    }
                    else
                    {
                        ClassicAssert.True( keyValuePair.Value );
                    }
                }
            }

        }

        [TestCaseSource( nameof( SwitchTestsCases ) )]
        public async Task can_turn_off_location_async( (string topic, List<string> deviceThatShouldBeFalse) tc )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOffCmd = pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                {
                    r.Topic = tc.topic;
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOffLocationCommand, ISwitchLocationCommandResult>( turnOffCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.ResultByDeviceName );

                foreach( var keyValuePair in result.ResultByDeviceName )
                {
                    if( tc.deviceThatShouldBeFalse.Contains( keyValuePair.Key ) )
                    {
                        ClassicAssert.False( keyValuePair.Value );
                    }
                    else
                    {
                        ClassicAssert.True( keyValuePair.Value );
                    }
                }
            }

        }

        public static IEnumerable<(string topic, List<string> deviceThatShouldBeFalse)> SwitchTestsCases()
        {
            yield return ("Test", new List<string>());
            yield return ("Test-FakeLEDStrip", new List<string>() { nameof( FakeSignatureDeviceHosts ) });
            yield return ("Test-FakeSignatureDevice", new List<string>() { nameof( FakeLEDStripHosts ) });
            yield return ("Unknown", new List<string>() { nameof( FakeLEDStripHosts ), nameof( FakeSignatureDeviceHosts ) });
        }

        [TestCaseSource( nameof( MultipleSwitchTestsCases ) )]
        public async Task can_turn_on_multiple_location_async( Dictionary<string, List<Switch>> tc )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var list = new List<ITurnOnLocationCommand>();
                foreach( var item in tc )
                {
                    list.Add( pocoDirectory.Create<ITurnOnLocationCommand>( r =>
                    {
                        r.Topic = item.Key;
                        r.Colors.Add( ColorLocation.Red );
                    } ) );
                }

                var turnOnMultipleCmd = pocoDirectory.Create<ITurnOnMultipleLocationsCommand>( r =>
                {
                    r.Locations.AddRange( list );
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOnMultipleLocationsCommand, ISwitchMultipleLocationsCommandResult>( turnOnMultipleCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.Results );

                foreach( var switchLocationCommandResult in result.Results )
                {
                    var allDevicesOnTopics = tc[switchLocationCommandResult.Topic];
                    ClassicAssert.NotNull( allDevicesOnTopics );
                    ClassicAssert.IsNotEmpty( allDevicesOnTopics );

                    foreach( var keyValuePair in switchLocationCommandResult.ResultByDeviceName )
                    {
                        var device = allDevicesOnTopics.First( x => x.DeviceName == keyValuePair.Key );

                        Assert.That( device.ShouldSuccess, Is.EqualTo( keyValuePair.Value ) );
                    }
                }
            }

        }

        [TestCaseSource( nameof( MultipleSwitchTestsCases ) )]
        public async Task can_turn_off_multiple_location_async( Dictionary<string, List<Switch>> tc )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var list = new List<ITurnOffLocationCommand>();
                foreach( var item in tc )
                {
                    list.Add( pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                    {
                        r.Topic = item.Key;
                    } ) );
                }

                var turnOffMultipleCmd = pocoDirectory.Create<ITurnOffMultipleLocationsCommand>( r =>
                {
                    r.Locations.AddRange( list );
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOffMultipleLocationsCommand, ISwitchMultipleLocationsCommandResult>( turnOffMultipleCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.Results );

                foreach( var switchLocationCommandResult in result.Results )
                {
                    var allDevicesOnTopics = tc[switchLocationCommandResult.Topic];
                    ClassicAssert.NotNull( allDevicesOnTopics );
                    ClassicAssert.IsNotEmpty( allDevicesOnTopics );

                    foreach( var keyValuePair in switchLocationCommandResult.ResultByDeviceName )
                    {
                        var device = allDevicesOnTopics.First( x => x.DeviceName == keyValuePair.Key );

                        Assert.That( device.ShouldSuccess, Is.EqualTo( keyValuePair.Value ) );
                    }
                }
            }

        }

        public static IEnumerable<Dictionary<string, List<Switch>>> MultipleSwitchTestsCases()
        {
            yield return new Dictionary<string, List<Switch>>()
            {
                {
                   "Test-FakeLEDStrip",
                   new List<Switch>(){
                       new Switch
                       {
                           DeviceName = nameof(FakeLEDStripHosts)
                       },
                       new Switch
                       {
                           DeviceName = nameof(FakeSignatureDeviceHosts),
                           ShouldSuccess = false
                       }
                   }
                },
                {
                   "Test-FakeSignatureDevice",
                   new List<Switch>(){
                       new Switch
                       {
                           DeviceName = nameof(FakeSignatureDeviceHosts)
                       },
                       new Switch
                       {
                           DeviceName = nameof(FakeLEDStripHosts),
                           ShouldSuccess = false
                       }
                   }
                }
            };
        }

        [TestCase( "Test", nameof( FakeLEDStripHosts ), true )]
        [TestCase( "Test", nameof( FakeSignatureDeviceHosts ), true )]
        [TestCase( "Test-FakeLEDStrip", nameof( FakeSignatureDeviceHosts ), false )]
        [TestCase( "Test-FakeLEDStrip", nameof( FakeLEDStripHosts ), true )]
        [TestCase( "Test-FakeSignatureDevice", nameof( FakeSignatureDeviceHosts ), true )]
        [TestCase( "Test-FakeSignatureDevice", nameof( FakeLEDStripHosts ), false )]
        public async Task can_force_turn_on_location_specific_device_async( string topic, string deviceFullName, bool isSuccess )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOnCmd = pocoDirectory.Create<ITurnOnLocationCommand>( r =>
                {
                    r.Topic = topic;
                    r.Colors.Add( ColorLocation.Red );
                    r.DeviceFullName = deviceFullName;
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOnLocationCommand, ISwitchLocationCommandResult>( turnOnCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.ResultByDeviceName );
                Assert.That( result.ResultByDeviceName.Count, Is.EqualTo( 1 ) );

                Assert.That( result.ResultByDeviceName[deviceFullName], Is.EqualTo( isSuccess ) );
            }
        }

        [TestCase( "Test", nameof( FakeLEDStripHosts ), true )]
        [TestCase( "Test", nameof( FakeSignatureDeviceHosts ), true )]
        [TestCase( "Test-FakeLEDStrip", nameof( FakeSignatureDeviceHosts ), false )]
        [TestCase( "Test-FakeLEDStrip", nameof( FakeLEDStripHosts ), true )]
        [TestCase( "Test-FakeSignatureDevice", nameof( FakeSignatureDeviceHosts ), true )]
        [TestCase( "Test-FakeSignatureDevice", nameof( FakeLEDStripHosts ), false )]
        public async Task can_turn_off_location_specific_device_async( string topic, string deviceFullName, bool isSuccess )
        {
            using( var scope = _auto.Services.CreateScope() )
            {
                var cbe = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
                var pocoDirectory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();

                var turnOffCmd = pocoDirectory.Create<ITurnOffLocationCommand>( r =>
                {
                    r.Topic = topic;
                    r.DeviceFullName = deviceFullName;
                } );
                var result = await CrisHelper.SendCrisCommandWithResultAsync<ITurnOffLocationCommand, ISwitchLocationCommandResult>( turnOffCmd, TestHelper.Monitor, cbe );

                ClassicAssert.NotNull( result );
                ClassicAssert.IsNotEmpty( result.ResultByDeviceName );
                Assert.That( result.ResultByDeviceName.Count, Is.EqualTo( 1 ) );

                Assert.That( result.ResultByDeviceName[deviceFullName], Is.EqualTo( isSuccess ) );
            }
        }

    }


    public class Switch
    {
        public string DeviceName { get; set; }
        public bool ShouldSuccess { get; set; } = true;
    }
}


