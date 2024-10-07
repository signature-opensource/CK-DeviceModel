using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using FluentAssertions;
using FluentAssertions.Equivalency;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests;

[TestFixture]
public class LogFromCommandAndEventLoopTests
{
    [Test]
    public async Task check_final_logs_Async()
    {
        using var gLog = TestHelper.Monitor.OpenInfo( nameof( check_final_logs_Async ) );
        // We don't have an easy way to inject a test GrandOuputSinkHandler: we build a new GrandOuput
        // with a text handler, make it the auto registering any new monitor and read the output file.
        var folder = TestHelper.LogFolder.AppendPart( "LogFromCommandAndEventLoop" );
        TestHelper.CleanupFolder( folder, ensureFolderAvailable: true );

        await using( var g = new GrandOutput( new GrandOutputConfiguration() { TrackUnhandledExceptions = true, Handlers = { new TextFileConfiguration() { Path = folder } } } ) )
        {
            Action<IActivityMonitor> autoConfig = m => g.EnsureGrandOutputClient( m );
            ActivityMonitor.AutoConfiguration += autoConfig;

            IDeviceHost host = new ScaleHost();
            var config = new CommonScaleConfiguration()
            {
                Name = "M",
                Status = DeviceConfigurationStatus.Runnable
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );
            var scale = (IActiveDevice?)host.Find( "M" );
            Debug.Assert( scale != null );

            var cmd = new ScaleTestSendLogsFromCommandAndEventLoopCommand() { DeviceName = "M" };
            scale.SendCommand( TestHelper.Monitor, cmd );
            await cmd.Completion;

            await host.ClearAsync( TestHelper.Monitor, true );

            ActivityMonitor.AutoConfiguration -= autoConfig;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Directory.GetFiles( folder ).Should().HaveCount( 1 );
        var lines = File.ReadAllText( Directory.GetFiles( folder )[0] ).Split( Environment.NewLine );
        for( int i = 0; i < 10; ++i )
        {
            lines.Where( l => l.Contains( $"Log from CommandLoop n°{i}." ) ).Should().HaveCount( 1 );
            lines.Where( l => l.Contains( $"Log from EventLoop n°{i}." ) ).Should().HaveCount( 1 );
        }
        // Checks that all ActivityMonitorExternalLogData and InputLogEntry are back to their
        // respective pool.
        ActivityMonitorExternalLogData.AliveCount.Should().Be( 0 );
        InputLogEntry.AliveCount.Should().Be( 0 );
    }
}
