using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Configuration.Tests
{
    [TestFixture]
    public class ExplicitConfigurationConstructorTests
    {
        [Test]
        public async Task with_root_properties_only_Async()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( with_root_properties_only_Async ) );

            var config = DynamicConfiguration.Create();
            config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:Status", "Runnable" );
            config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:OneProp", "I'm a property." );
            config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:AnotherProp", "3712" );

            var host = new ConveyorDeviceHost();
            ConveyorDevice? device = await CreateTheBeastDeviceAsync( config, host );
            device.ExternalConfiguration.OneProp.Should().Be( "I'm a property." );
            device.ExternalConfiguration.AnotherProp.Should().Be( 3712 );
            device.ExternalConfiguration.Hubs.Should().BeEmpty();

            await host.ClearAsync( TestHelper.Monitor, true );
        }

        [Test]
        public async Task from_json_with_hubs_Async()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( with_root_properties_only_Async ) );

            var builder = new ConfigurationBuilder();
            builder.AddJsonStream( new MemoryStream( Encoding.UTF8.GetBytes( @"
{
    ""CK-DeviceModel"": {
        ""ConveyorDeviceHost"": {
            ""Items"": {
                ""TheBeast"": {
                    ""Status"": ""Runnable"",
                    ""OneProp"": ""I have Hubs!"",
                    ""Hubs"": {
                        ""Starter"": {
                            ""IPAddress"": ""192.168.5.4:3712"",
                            ""TimeoutMilliseconds"": ""100"",
                            ""Components"": {
                                ""EntrySlide"": {
                                    ""Type"": ""Span"",
                                    ""Length"": ""30""
                                },
                                ""PrimaryController"": {
                                    ""Type"": ""Controller"",
                                    ""ManufacturerDeviceName"": ""BWU8745"",
                                    ""Address"": ""124"",
                                    ""Position"": ""30""
                                },
                                ""S1"":{
                                    ""Type"": ""BooleanSensor"",
                                    ""Position"": ""40"",
                                    ""ControllerName"": ""PrimaryController"",
                                    ""FieldName"": ""I1""
                                }, 
                                ""Motor1"": {
                                    ""Type"": ""Motor"",
                                    ""ControllerName"": ""PrimaryController"",
                                    ""Position"": ""30"",
                                    ""Length"": ""250"",
                                    ""ForwardFieldName"": ""SpeedM1""
                                },
                                ""S2"":{
                                    ""Type"": ""BooleanSensor"",
                                    ""Position"": ""270"",
                                    ""ControllerName"": ""PrimaryController"",
                                    ""FieldName"": ""I2""
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}" ) ) );
            var config = builder.Build();

            var host = new ConveyorDeviceHost();
            var hosts = new IDeviceHost[] { host };
            var daemon = new DeviceHostDaemon( hosts, new DefaultDeviceAlwaysRunningPolicy() );
            var configurator = new DeviceConfigurator( daemon, config );
            await ((IHostedService)configurator).StartAsync( default );

            var device = host.Find( "TheBeast" );
            Debug.Assert( device != null );
            var c = device.ExternalConfiguration;
            c.OneProp.Should().Be( "I have Hubs!" );
            c.Hubs.Should().HaveCount( 1 );
            c.Status.Should().Be( DeviceConfigurationStatus.Runnable );
            var h = c.Hubs["Starter"];
            h.IPAddress.Should().Be( "192.168.5.4:3712" );
            h.TimeoutMilliseconds.Should().Be( 100 );
            h.Components.Should().HaveCount( 5 );
            var entry = (SpanConfiguration)h.Components["EntrySlide"];
            var primaryController = (ControllerConfiguration)h.Components["PrimaryController"];
            var sensor1 = (BooleanSensorConfiguration)h.Components["S1"];
            var motor1 = (MotorConfiguration)h.Components["Motor1"];
            var sensor2 = (BooleanSensorConfiguration)h.Components["S2"];

            entry.Name.Should().Be( "EntrySlide" );
            entry.Length.Should().Be( 30 );
            entry.Position.Should().Be( 0 );

            primaryController.Name.Should().Be( "PrimaryController" );
            primaryController.Position.Should().Be( 30 );
            primaryController.Address.Should().Be( 124 );
            primaryController.ManufacturerDeviceName.Should().Be( "BWU8745" );

            sensor1.Name.Should().Be( "S1" );
            sensor1.Position.Should().Be( 40 );
            sensor1.ControllerName.Should().Be( "PrimaryController" );
            sensor1.FieldName.Should().Be( "I1" );

            motor1.Name.Should().Be( "Motor1" );
            motor1.Position.Should().Be( 30 );
            motor1.ControllerName.Should().Be( "PrimaryController" );
            motor1.Length.Should().Be( 250 );
            motor1.ForwardFieldName.Should().Be( "SpeedM1" );

            sensor2.Name.Should().Be( "S2" );
            sensor2.Position.Should().Be( 270 );
            sensor2.ControllerName.Should().Be( "PrimaryController" );
            sensor2.FieldName.Should().Be( "I2" );

            await host.ClearAsync( TestHelper.Monitor, true );
        }

        static async Task<ConveyorDevice> CreateTheBeastDeviceAsync( DynamicConfiguration config, ConveyorDeviceHost host )
        {
            var hosts = new IDeviceHost[] { host };
            var daemon = new DeviceHostDaemon( hosts, new DefaultDeviceAlwaysRunningPolicy() );
            var configurator = new DeviceConfigurator( daemon, config.Root );
            await ((IHostedService)configurator).StartAsync( default );

            var device = host.Find( "TheBeast" );
            Debug.Assert( device != null );
            return device;
        }

    }
}
