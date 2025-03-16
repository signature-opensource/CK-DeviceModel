using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CK.DeviceModel;

/// <summary>
/// This is the "restarter daemon" that allows devices with <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration
/// to be monitored and automatically restarted when stopped. See <see cref="IDeviceAlwaysRunningPolicy"/>.
/// <para>
/// This daemon can also destroy device when stopped (see <see cref="StoppedBehavior"/>).
/// </para>
/// </summary>
public sealed class DeviceHostDaemon : ISingletonAutoService, IHostedService
{
    // These two constructor signatures apply to DeviceHostConfiguration and DeviceConfiguration:
    // they enable configurations to take control of the configuration analysis instead of relying on
    // configuration binders.
    static readonly Type[] _sigMonitorAndSection = new Type[] { typeof( IActivityMonitor ), typeof( IConfigurationSection ) };
    static readonly Type[] _sigSectionType = new Type[] { typeof( IConfigurationSection ) };

    readonly IInternalDeviceHost[] _deviceHosts;
    readonly IDeviceAlwaysRunningPolicy _alwaysRunningPolicy;
    readonly CancellationTokenSource _stoppedTokenSource;

    Task? _runLoop;
    volatile TaskCompletionSource<bool> _signal;
    IActivityMonitor? _daemonMonitor;

    /// <summary>
    /// Initializes a new <see cref="DeviceHostDaemon"/>.
    /// </summary>
    /// <param name="deviceHosts">The available device hosts.</param>
    /// <param name="alwaysRunningPolicy">The policy that handles AlwaysRunning devices that stop.</param>
    public DeviceHostDaemon( IEnumerable<IDeviceHost> deviceHosts, IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
    {
        Throw.CheckNotNullArgument( alwaysRunningPolicy );
        _alwaysRunningPolicy = alwaysRunningPolicy;
        _stoppedTokenSource = new CancellationTokenSource();
        // See here https://stackoverflow.com/questions/28321457/taskcontinuationoptions-runcontinuationsasynchronously-and-stack-dives
        // why RunContinuationsAsynchronously is crucial.
        _signal = new TaskCompletionSource<bool>( TaskCreationOptions.RunContinuationsAsynchronously );
        _deviceHosts = deviceHosts.Cast<IInternalDeviceHost>().ToArray();
        // Don't wait for the Start: associate the daemon right now to each and every hosts.
        foreach( var h in _deviceHosts )
        {
            h.SetDaemon( this );
        }
    }

    /// <summary>
    /// Gets or sets whether when this service stops, all devices of all hosts must be destroyed.
    /// Defaults to <see cref="OnStoppedDaemonBehavior.None"/>.
    /// </summary>
    public OnStoppedDaemonBehavior StoppedBehavior { get; set; }

    /// <summary>
    /// Gets a cancellation token that will be signaled when this service is stopping.
    /// </summary>
    public CancellationToken StoppedToken => _stoppedTokenSource.Token;

    /// <summary>
    /// Gets all the device hosts that exist.
    /// </summary>
    public IReadOnlyList<IDeviceHost> DeviceHosts => _deviceHosts;

    /// <summary>
    /// Tries to apply a host configuration from a configuration section.
    /// The <see cref="IConfigurationSection.Key"/> must be the name of a Device host.
    /// <see cref="DeviceHost{TConfiguration}.ApplyConfigurationAsync(IActivityMonitor, THostConfiguration, bool)"/> is called:
    /// if <paramref name="isPartialConfiguration"/> is false, devices for which no configuration appear are stopped and destroyed.
    /// <para>
    /// ApplyConfigurationAsync is called with a true <c>allowEmptyConfiguration</c> parameter: this allows an empty
    /// configuration, when <paramref name="isPartialConfiguration"/> is also true, to fully remove any devices from a host.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="c">The configuration section to apply.</param>
    /// <param name="isPartialConfiguration">
    /// True to only reconfigure the devices that appear in the configuration. False to stop and destroy any devices
    /// that don't appear in the configuration.
    /// </param>
    /// <returns>True if host has been found, the configuration is valid and has been applied successfully. False otherwise.</returns>
    public Task<bool> ReconfigureHostAsync( IActivityMonitor monitor, IConfigurationSection configuration, bool isPartialConfiguration )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( configuration );
        var (host, hostConfig) = CreateHostConfiguration( monitor, configuration );
        if( host == null )
        {
            monitor.Error( $"Configuration key '{configuration.Key}' is not a known host. Available hosts are: {_deviceHosts.Select( device => device.DeviceHostName ).Concatenate()}." );
        }
        if( hostConfig == null )
        {
            return Task.FromResult( false );
        }
        hostConfig.IsPartialConfiguration = isPartialConfiguration;
        Debug.Assert( host != null, "Since we have a hostConfig." );
        return host.ApplyConfigurationAsync( monitor, hostConfig, allowEmptyConfiguration: true );
    }

    /// <summary>
    /// Tries to map the <see cref="IConfigurationSection"/> to a host and its configuration.
    /// The <see cref="IConfigurationSection.Key"/> must be the name of a Device host, if it is not found,
    /// <c>(null,null)</c> is returned without any error or warning.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="c">The configuration section to analyze.</param>
    /// <returns>The host if found and it configuration if no error occurred.</returns>
    public (IDeviceHost? DeviceHost, IDeviceHostConfiguration? HostConfiguration) CreateHostConfiguration( IActivityMonitor monitor, IConfigurationSection c )
    {
        var deviceHost = _deviceHosts.FirstOrDefault( h => h.DeviceHostName.Equals( c.Key, StringComparison.OrdinalIgnoreCase ) );
        if( deviceHost == null )
        {
            return (null, null);
        }
        IDeviceHostConfiguration? config = null;
        using( monitor.OpenInfo( $"Analyzing DeviceHost '{c.Key}' configuration." ) )
        {
            Type tHostConfig = deviceHost.GetDeviceHostConfigurationType();
            if( !FindSpecificConstructors( monitor,
                                           tHostConfig,
                                           out var ctorHost0,
                                           out var ctorHost1,
                                           out var ctorHost2 ) )
            {
                // No constructor found. This is a serious error.
                return (deviceHost, null);
            }
            if( ctorHost2 != null )
            {
                config = (IDeviceHostConfiguration)ctorHost2.Invoke( new object[] { monitor, c } );
            }
            else if( ctorHost1 != null )
            {
                config = (IDeviceHostConfiguration)ctorHost1.Invoke( new object[] { c } );
            }
            else
            {
                Debug.Assert( ctorHost0 != null );
                // We use the standard Bind: all the properties that can be bound will be.
                // But we don't want the Items to be bound since we take control of it below (DeviceConfiguration
                // can have dedicated constructors).
                //
                // The CreateSectionWithout is a small helper that does the magic.
                //
                // We MUST use Bind on an existing instance here instead of a simple Get( tHostConfig ) because
                // if "Items" is the ONLY property (that is more the rule than exception), then Get returns
                // null since it sees a totally empty section! 
                config = (IDeviceHostConfiguration)ctorHost0.Invoke( Array.Empty<object>() );
                c.CreateSectionWithout( nameof( IDeviceHostConfiguration.Items ) ).Bind( config );
            }

            var items = c.GetSection( nameof( IDeviceHostConfiguration.Items ) );
            if( !items.Exists() )
            {
                monitor.Warn( $"No 'Items' section found." );
            }
            else
            {
                Type deviceConfigType = deviceHost.GetDeviceConfigurationType();
                if( !FindSpecificConstructors( monitor,
                                               deviceConfigType,
                                               out ConstructorInfo? ctor0,
                                               out ConstructorInfo? ctor1,
                                               out ConstructorInfo? ctor2 ) )
                {
                    // No constructor found. This is a serious error.
                    return (deviceHost, null);
                }
                foreach( var deviceConfig in items.GetChildren() )
                {
                    DeviceConfiguration? configObject = null;
                    monitor.Debug( $"Handling Device item: {deviceConfig.Key}." );
                    try
                    {
                        if( ctor2 != null )
                        {
                            configObject = (DeviceConfiguration?)ctor2.Invoke( new object[] { monitor, deviceConfig } );
                        }
                        else if( ctor1 != null )
                        {
                            configObject = (DeviceConfiguration?)ctor1.Invoke( new object[] { deviceConfig } );
                        }
                        else
                        {
                            configObject = (DeviceConfiguration?)deviceConfig.Get( deviceConfigType );
                        }
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( $"While instantiating Device configuration for '{deviceConfig.Path}' and type '{deviceConfigType:C}'.", ex );
                    }
                    if( configObject == null )
                    {
                        monitor.Warn( $"Unable to bind configuration entry '{deviceConfig.Key}'." );
                    }
                    else
                    {
                        configObject.Name = deviceConfig.Key;
                        config.Add( configObject );
                    }
                }
            }
        }
        return (deviceHost, config);


    }

    internal static bool FindSpecificConstructors( IActivityMonitor monitor,
                                                   Type configType,
                                                   out ConstructorInfo? defaultCtor,
                                                   out ConstructorInfo? sectionOnly,
                                                   out ConstructorInfo? monitorAndSection )
    {
        defaultCtor = sectionOnly = null;
        if( (monitorAndSection = configType.GetConstructor( _sigMonitorAndSection )) != null )
        {
            monitor.Debug( $"Found constructor {configType:C}( IActivityMonitor, IConfigurationSection )." );
        }
        else if( (sectionOnly = configType.GetConstructor( _sigSectionType )) != null )
        {
            monitor.Debug( $"Found constructor {configType:C}( IConfigurationSection )." );
        }
        else if( (defaultCtor = configType.GetConstructor( Type.EmptyTypes )) != null )
        {
            monitor.Debug( $"Using default {configType:C} constructor and configuration binding." );
        }
        else
        {
            monitor.Error( $"Failed to locate a valid constructor on '{configType:C}' type." );
            return false;
        }
        return true;
    }


    Task IHostedService.StartAsync( CancellationToken cancellationToken )
    {
        if( _deviceHosts.Length > 0 )
        {
            _daemonMonitor = new ActivityMonitor( nameof( DeviceHostDaemon ) );
            _runLoop = TheLoopAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Triggers the current source after having initiated the next one.
    /// </summary>
    internal void Signal()
    {
        var s = _signal;
        _signal = new TaskCompletionSource<bool>( TaskCreationOptions.RunContinuationsAsynchronously );
        s.TrySetResult( true );
    }

    async Task TheLoopAsync()
    {
        Debug.Assert( _daemonMonitor != null );
        _daemonMonitor.Debug( "Daemon loop started." );
        while( !_stoppedTokenSource.IsCancellationRequested )
        {
            var signalTask = _signal.Task;
            using( _daemonMonitor.OpenDebug( "Checking always running devices." ) )
            {
                DateTime now = DateTime.UtcNow;
                long wait = Int64.MaxValue;
                foreach( var h in _deviceHosts )
                {
                    var delta = await h.DaemonCheckAlwaysRunningAsync( _daemonMonitor, _alwaysRunningPolicy, now ).ConfigureAwait( false );
                    Debug.Assert( delta > 0 );
                    if( wait > delta ) wait = delta;
                }
                if( signalTask.IsCompleted )
                {
                    _daemonMonitor.CloseGroup( $"Daemon has been signaled. Repeat." );
                }
                else if( wait != Int64.MaxValue )
                {
                    int w = (int)(wait / TimeSpan.TicksPerMillisecond);
                    if( w > 2 )
                    {
                        _daemonMonitor.CloseGroup( $"Waiting for {w} ms or a signal." );
                        await signalTask.WaitForTaskCompletionAsync( w ).ConfigureAwait( false );
                    }
                    else
                    {
                        _daemonMonitor.CloseGroup( $"Should wait for a ridiculous {w} ms time. Repeat immediately." );
                    }
                }
                else
                {
                    _daemonMonitor.CloseGroup( $"Waiting for a signal." );
                    await signalTask.ConfigureAwait( false );
                }
            }
        }
        if( StoppedBehavior != OnStoppedDaemonBehavior.None )
        {
            using( _daemonMonitor.OpenInfo( $"End of Daemon loop: StoppedBehavior is '{StoppedBehavior}'." ) )
            {
                // We may catch a OperationCanceledException here.
                try
                {
                    foreach( var h in _deviceHosts )
                    {
                        await h.ClearAsync( _daemonMonitor, waitForDeviceDestroyed: StoppedBehavior == OnStoppedDaemonBehavior.ClearAllHostsAndWaitForDevicesDestroyed )
                               .ConfigureAwait( false );
                    }
                }
                catch( Exception ex )
                {
                    _daemonMonitor.Error( ex );
                }
            }
        }
        _daemonMonitor.MonitorEnd();
    }

    async Task IHostedService.StopAsync( CancellationToken cancellationToken )
    {
        if( _deviceHosts.Length > 0 )
        {
            Debug.Assert( _runLoop != null );
            await _stoppedTokenSource.CancelAsync();
            Signal();
            await _runLoop.WaitForTaskCompletionAsync( Timeout.Infinite, cancellationToken );
        }
    }
}
