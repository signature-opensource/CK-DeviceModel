using Shouldly;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Configuration.Tests;

[TestFixture]
public class ExplicitConfigurationConstructorTests
{
    [TestCase( "UseBinding" )]
    [TestCase( "" )]
    public async Task with_root_properties_only_Async( string mode )
    {
        ConveyorDeviceConfiguration.UseBinding = mode == "UseBinding";

        var config = DynamicConfiguration.Create();
        config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:Status", "Runnable" );
        config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:OneProp", "I'm a property." );
        config.Provider.Set( "CK-DeviceModel:ConveyorDeviceHost:Items:TheBeast:AnotherProp", "3712" );

        var host = new ConveyorDeviceHost();
        ConveyorDevice? device = await CreateTheBeastDeviceAsync( config, host );
        device.ExternalConfiguration.OneProp.ShouldBe( "I'm a property." );
        device.ExternalConfiguration.AnotherProp.ShouldBe( 3712 );
        device.ExternalConfiguration.Hubs.ShouldBeEmpty();

        await host.ClearAsync( TestHelper.Monitor, true );
    }

    [TestCase( "UseBinding" )]
    [TestCase( "" )]
    public async Task from_json_with_hubs_Async( string mode )
    {
        ConveyorDeviceConfiguration.UseBinding = mode == "UseBinding";

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
        c.OneProp.ShouldBe( "I have Hubs!" );
        c.Hubs.Count.ShouldBe( 1 );
        c.Status.ShouldBe( DeviceConfigurationStatus.Runnable );
        var h = c.Hubs["Starter"];
        h.IPAddress.ShouldBe( "192.168.5.4:3712" );
        h.TimeoutMilliseconds.ShouldBe( 100 );
        h.Components.Count.ShouldBe( 5 );
        var entry = (SpanConfiguration)h.Components["EntrySlide"];
        var primaryController = (ControllerConfiguration)h.Components["PrimaryController"];
        var sensor1 = (BooleanSensorConfiguration)h.Components["S1"];
        var motor1 = (MotorConfiguration)h.Components["Motor1"];
        var sensor2 = (BooleanSensorConfiguration)h.Components["S2"];

        entry.Name.ShouldBe( "EntrySlide" );
        entry.Length.ShouldBe( 30 );
        entry.Position.ShouldBe( 0 );

        primaryController.Name.ShouldBe( "PrimaryController" );
        primaryController.Position.ShouldBe( 30 );
        primaryController.Address.ShouldBe( 124 );
        primaryController.ManufacturerDeviceName.ShouldBe( "BWU8745" );

        sensor1.Name.ShouldBe( "S1" );
        sensor1.Position.ShouldBe( 40 );
        sensor1.ControllerName.ShouldBe( "PrimaryController" );
        sensor1.FieldName.ShouldBe( "I1" );

        motor1.Name.ShouldBe( "Motor1" );
        motor1.Position.ShouldBe( 30 );
        motor1.ControllerName.ShouldBe( "PrimaryController" );
        motor1.Length.ShouldBe( 250 );
        motor1.ForwardFieldName.ShouldBe( "SpeedM1" );

        sensor2.Name.ShouldBe( "S2" );
        sensor2.Position.ShouldBe( 270 );
        sensor2.ControllerName.ShouldBe( "PrimaryController" );
        sensor2.FieldName.ShouldBe( "I2" );

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
