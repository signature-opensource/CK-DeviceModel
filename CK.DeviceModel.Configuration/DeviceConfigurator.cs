using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                NoItemsSectionWrapper noItemsSection = new NoItemsSectionWrapper();
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
                    foreach( var c in _configuration.GetChildren() )
                    {
                        if( c.Key == "Daemon" )
                        {
                            HandleDaemonConfiguration( c );
                            continue;
                        }
                        var d = _deviceHosts.FirstOrDefault( h => h.DeviceHostName == c.Key );
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
                                var ctor = tHostConfig.GetConstructor( new Type[] { typeof( IActivityMonitor ), typeof( IConfiguration ) } );
                                if( ctor != null )
                                {
                                    _changeMonitor.Debug( $"Found constructor( IActivityMonitor, IConfiguration )." );
                                    config = (IDeviceHostConfiguration)ctor.Invoke( new object[] { _changeMonitor, c } );
                                }
                                else
                                {
                                    ctor = tHostConfig.GetConstructor( new Type[] { typeof( IConfiguration ) } );
                                    if( ctor != null )
                                    {
                                        _changeMonitor.Debug( $"Found constructor( IConfiguration )." );
                                        config = (IDeviceHostConfiguration)ctor.Invoke( new object[] { c } );
                                    }
                                    else
                                    {
                                        ctor = tHostConfig.GetConstructor( Type.EmptyTypes );
                                        if( ctor != null )
                                        {
                                            _changeMonitor.Debug( $"Using default constructor and configuration binding." );
                                            config = (IDeviceHostConfiguration)ctor.Invoke( Array.Empty<object>() );
                                            noItemsSection.InnerSection = c;
                                            noItemsSection.Bind( config );
                                        }
                                        else
                                        {
                                            _changeMonitor.Error( $"Failed to locate a valid constructor on '{tHostConfig.FullName}' type." );
                                            continue;
                                        }
                                        var items = c.GetSection( nameof( IDeviceHostConfiguration.Items ) );
                                        if( !items.Exists() )
                                        {
                                            _changeMonitor.Warn( $"No 'Items' section found." );
                                        }
                                        else
                                        {
                                            foreach( var deviceConfig in items.GetChildren() )
                                            {
                                                _changeMonitor.Debug( $"Handling Device item: {deviceConfig.Key}." );
                                                var configObject = (DeviceConfiguration)deviceConfig.Get( d.GetDeviceConfigurationType() );
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

        class NoItemsSectionWrapper : IConfigurationSection
        {
            class Empty : IConfigurationSection
            {
                readonly IConfiguration _c;

                public Empty( IConfigurationSection c, string key )
                {
                    Path = ConfigurationPath.Combine( c.Path, key );
                    Key = key;
                    _c = c;
                }

                public string? this[string key] { get => null; set => throw new NotSupportedException(); }

                public string Key { get; }

                public string Path { get; }

                public string? Value { get => null; set => throw new NotSupportedException(); }

                public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

                public IChangeToken GetReloadToken() => _c.GetReloadToken();

                public IConfigurationSection GetSection( string key ) => new Empty( this, key );
            }

            static readonly string _skippedLeaf = ConfigurationPath.KeyDelimiter + nameof(IDeviceHostConfiguration.Items );

            public IConfigurationSection InnerSection = null!;

            public string Key => InnerSection.Key;

            public string Path => InnerSection.Path;

            public string? Value
            {
                get => InnerSection.Value;
                set => throw new NotSupportedException();
            }

            public string? this[string key]
            {
                get => key == nameof(IDeviceHostConfiguration.Items) ? null : InnerSection[key];
                set => throw new NotSupportedException();
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                return InnerSection.GetChildren().Where( c => !c.Path.EndsWith( _skippedLeaf ) );
            }

            public IChangeToken GetReloadToken() => InnerSection.GetReloadToken();

            public IConfigurationSection GetSection( string key )
            {
                return key != nameof( IDeviceHostConfiguration.Items ) ? InnerSection.GetSection( key ) : new Empty( this, key );
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
