using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.DeviceModel
{
    /// <summary>
    /// Handles the "Device" configuration section (if it exists) by configuring all the <see cref="IDeviceHost"/> available in the DI Container
    /// and supports the "restarter daemon" that allows devices with <see cref="DeviceConfigurationStatus.AlwaysRunning"/> configuration
    /// to be monitored and automatically restarted when stopped (see <see cref="IDeviceAlwaysRunningPolicy"/>).
    /// </summary>
    public class DeviceConfigurator : ISingletonAutoService, IHostedService
    {
        readonly IConfigurationSection _configuration;
        readonly IConfiguration _configurationRoot;
        readonly IDeviceHost[] _deviceHosts;
        readonly CancellationTokenSource _run;
        readonly IActivityMonitor _changeMonitor;
        readonly DeviceHostDaemon _daemon;
        IDisposable? _changeSubscription;
        readonly Channel<(IDeviceHost, IDeviceHostConfiguration)[]> _applyChannel;

        /// <summary>
        /// Initializes a new <see cref="DeviceConfigurator"/>.
        /// </summary>
        /// <param name="daemon">The daemon.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="deviceHosts">The available hosts.</param>
        public DeviceConfigurator( DeviceHostDaemon daemon, IConfiguration configuration, IEnumerable<IDeviceHost> deviceHosts )
        {
            _daemon = daemon;
            _configurationRoot = configuration;
            _changeMonitor = new ActivityMonitor( "CK-DeviceModel Configurator (Initializing)" );
            _changeMonitor.AutoTags += IDeviceHost.DeviceModel;

            _run = new CancellationTokenSource();
            _configuration = configuration.GetSection( "CK-DeviceModel" );
            _deviceHosts = deviceHosts.ToArray();
            _applyChannel = Channel.CreateUnbounded<(IDeviceHost, IDeviceHostConfiguration)[]>( new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true } );
            // If the daemon is stopped before us, we preemptively stops the watch.
            daemon.StoppedToken.Register( DoStop );
        }

        async Task IHostedService.StartAsync( CancellationToken cancellationToken )
        {
            OnConfigurationChanged();
            if( _applyChannel.Reader.TryRead( out var toApply ))
            {
                await ApplyOnceAsync( _changeMonitor, toApply );
            }
            _changeMonitor.SetTopic( "CK-DeviceModel Configurator (Configuration change detection)" );
            _changeSubscription = ChangeToken.OnChange( _configuration.GetReloadToken, OnConfigurationChanged );
            _ = Task.Run( TheLoopAsync, cancellationToken: default );
        }

        async Task TheLoopAsync()
        {
            var tRun = _run.Token;
            while( !_run.IsCancellationRequested )
            {
                var toApply = await _applyChannel.Reader.ReadAsync( tRun );
                await ApplyOnceAsync( _changeMonitor, toApply );
            }
            _changeMonitor.MonitorEnd();
        }

        static async Task ApplyOnceAsync( IActivityMonitor monitor, (IDeviceHost, IDeviceHostConfiguration)[] toApply )
        {
            foreach( var (host, config) in toApply )
            {
                if( host == null ) break;
                try
                {
                    await host.ApplyConfigurationAsync( monitor, config );
                }
                catch( Exception ex )
                {
                    monitor.Error( "While applying DeviceHost configuration.", ex );
                }
            }
        }

        void OnConfigurationChanged()
        {
            using( _changeMonitor.OpenInfo( "Building configuration objects for Devices." ) )
            {
                try
                {
                    if( !_configuration.Exists() )
                    {
                        var foundDevice = _configurationRoot.GetSection( "Device" ).Exists();
                        var foundDeviceModel = _configurationRoot.GetSection( "DeviceModel" ).Exists();
                        var foundCKDeviceModel = _configurationRoot.GetSection( "CKDeviceModel" ).Exists();
                        _changeMonitor.Warn( "Missing 'CK-DeviceModel' configuration section. No devices to configure." );
                        if( foundDevice || foundDeviceModel || foundCKDeviceModel )
                        {
                            _changeMonitor.Error( $"Configuration section must be 'CK-DeviceModel'. A '{(foundDevice ? "Device" : (foundDeviceModel ? "DeviceModel" : "CKDeviceModel"))}' section exists. It should be renamed." );
                        }
                        return;
                    }
                    int idxResult = 0;
                    (IDeviceHost, IDeviceHostConfiguration)[]? toApply = null;

                    Type[] sigMonitorAndSection = new Type[] { typeof( IActivityMonitor ), typeof( IConfigurationSection ) };
                    Type[] sigSectionType = new Type[] { typeof( IConfigurationSection ) };

                    foreach( var c in _configuration.GetChildren() )
                    {
                        if( c.Key.Equals( "Daemon", StringComparison.OrdinalIgnoreCase ) )
                        {
                            HandleDaemonConfiguration( c );
                            continue;
                        }
                        var d = _deviceHosts.FirstOrDefault( h => h.DeviceHostName.Equals( c.Key, StringComparison.OrdinalIgnoreCase ) );
                        if( d == null )
                        {
                            _changeMonitor.Warn( $"DeviceHost named '{c.Key}' not found. It is ignored. Available hosts are: {_deviceHosts.Select( device => device.DeviceHostName ).Concatenate()}." );
                        }
                        else
                        {

                            using( _changeMonitor.OpenInfo( $"Handling DeviceHost '{c.Key}'." ) )
                            {
                                IDeviceHostConfiguration config;

                                Type tHostConfig = d.GetDeviceHostConfigurationType();
                                if( !FindSpecificConstructors( sigMonitorAndSection, sigSectionType, tHostConfig, out var ctorHost0, out var ctorHost1, out var ctorHost2) )
                                {
                                    continue;
                                }
                                if( ctorHost2 != null )
                                {
                                    config = (IDeviceHostConfiguration)ctorHost2.Invoke( new object[] { _changeMonitor, c } );
                                }
                                else if( ctorHost1 != null )
                                {
                                    config = (IDeviceHostConfiguration)ctorHost1.Invoke( new object[] { c } );
                                }
                                else
                                {
                                    Debug.Assert( ctorHost0 != null );
                                    // We use the standard Bind: all the properties than can be bound will be.
                                    // But we don't want the Items to be bound since we take control of it below (DeviceConfiguration
                                    // can have dedicated constructors). The CreateSectionWithout is a small helper that does the magic.
                                    //
                                    // We MUST use Bind on an existing instance here instead of a simple Get( tHostConfig ) because
                                    // if "Items" is the ONLY property (that is the more the rule than exception), then Get returns
                                    // null since it sees a totally empty section! 
                                    config = (IDeviceHostConfiguration)ctorHost0.Invoke( Array.Empty<object>() );
                                    c.CreateSectionWithout( nameof( IDeviceHostConfiguration.Items ) ).Bind( config );
                                }
                                var ctor = tHostConfig.GetConstructor( sigMonitorAndSection );
                                if( ctor != null )
                                {
                                    _changeMonitor.Debug( $"Found constructor {tHostConfig:C}( IActivityMonitor, IConfigurationSection )." );
                                }
                                else
                                {
                                    var items = c.GetSection( nameof( IDeviceHostConfiguration.Items ) );
                                    if( !items.Exists() )
                                    {
                                        _changeMonitor.Warn( $"No 'Items' section found." );
                                    }
                                    else
                                    {
                                        Type deviceConfigType = d.GetDeviceConfigurationType();
                                        if( !FindSpecificConstructors( sigMonitorAndSection,
                                                                       sigSectionType,
                                                                       deviceConfigType,
                                                                       out ConstructorInfo? ctor0,
                                                                       out ConstructorInfo? ctor1,
                                                                       out ConstructorInfo? ctor2 ) )
                                        {
                                            continue;
                                        }
                                        foreach( var deviceConfig in items.GetChildren() )
                                        {
                                            DeviceConfiguration? configObject = null;
                                            _changeMonitor.Debug( $"Handling Device item: {deviceConfig.Key}." );
                                            try
                                            {
                                                if( ctor2 != null )
                                                {
                                                    configObject = (DeviceConfiguration?)ctor2.Invoke( new object[] { _changeMonitor, deviceConfig } );
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
                                                _changeMonitor.Error( $"While instantiating Device configuration for '{deviceConfig.Path}' and type '{deviceConfigType:C}'.", ex );
                                            }
                                            if( configObject == null )
                                            {
                                                _changeMonitor.Warn( $"Unable to bind configuration entry '{deviceConfig.Key}'." );
                                            }
                                            else
                                            {
                                                configObject.Name = deviceConfig.Key;
                                                config.Add( configObject );
                                            }
                                        }
                                    }
                                    if( toApply == null ) toApply = new (IDeviceHost, IDeviceHostConfiguration)[_deviceHosts.Length];
                                    toApply[idxResult++] = (d, config);
                                }
                            }
                        }
                    }
                    if( toApply != null )
                    {
                        _changeMonitor.CloseGroup( $"{idxResult} device hosts must be reconfigured." );
                        _applyChannel.Writer.TryWrite( toApply );
                    }
                    else
                    {
                        _changeMonitor.CloseGroup( "No device hosts configuration." );
                    }
                }
                catch( Exception ex )
                {
                    _changeMonitor.Error( ex );
                }
            }
        }

        bool FindSpecificConstructors( Type[] sigMonitorAndSection,
                                       Type[] sigSectionType,
                                       Type configType,
                                       out ConstructorInfo? ctor0,
                                       out ConstructorInfo? ctor1,
                                       out ConstructorInfo? ctor2 )
        {
            ctor0 = ctor1 = ctor2 = null;
            if( (ctor2 = configType.GetConstructor( sigMonitorAndSection )) != null )
            {
                _changeMonitor.Debug( $"Found constructor {configType:C}( IActivityMonitor, IConfigurationSection )." );
            }
            else if( (ctor1 = configType.GetConstructor( sigSectionType )) != null )
            {
                _changeMonitor.Debug( $"Found constructor {configType:C}( IConfigurationSection )." );
            }
            else if( (ctor0 = configType.GetConstructor( Type.EmptyTypes )) != null )
            {
                _changeMonitor.Debug( $"Using default {configType:C} constructor and configuration binding." );
            }
            else
            {
                _changeMonitor.Error( $"Failed to locate a valid constructor on '{configType:C}' type." );
                return false;
            }
            return true;
        }

        void HandleDaemonConfiguration( IConfigurationSection daemonSection )
        {
            Debug.Assert( nameof( DeviceHostDaemon.StoppedBehavior ) == "StoppedBehavior", "This should not be changed since it's used in configuration!" );
            foreach( var daemonConfig in daemonSection.GetChildren() )
            {
                if( daemonConfig.Key == "StoppedBehavior" )
                {
                    if( !Enum.TryParse<OnStoppedDaemonBehavior>( daemonConfig.Value, out var behavior ) )
                    {
                        var validNames = string.Join( ", ", Enum.GetNames( typeof( OnStoppedDaemonBehavior ) ) );
                        _changeMonitor.Warn( $"Invalid Daemon.StoppedBehavior configuration '{daemonConfig.Value}'. Keeping current '{_daemon.StoppedBehavior}' behavior. Valid values are: {validNames}." );
                    }
                    else if( _daemon.StoppedBehavior != behavior )
                    {
                        _changeMonitor.Trace( $"Daemon.StoppedBehavior set to '{_daemon.StoppedBehavior}'." );
                        _daemon.StoppedBehavior = behavior;
                    }
                }
                else
                {
                    _changeMonitor.Warn( $"Skipped invalid Daemon configuration key '{daemonSection.Key}'. Valid keys are 'StoppedBehavior'." );
                }
            }
        }


        Task IHostedService.StopAsync( CancellationToken cancellationToken )
        {
            DoStop();
            return Task.CompletedTask;
        }

        void DoStop()
        {
            _run.Cancel();
            _changeSubscription?.Dispose();
        }
    }        
}
