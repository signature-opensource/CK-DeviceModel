using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.DeviceModel
{
    /// <summary>
    /// Extends <see cref="IConfigurationSection"/> interface.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Creates a section that hides a child property (that can be a simple item or a full section).
        /// <para>
        /// This is required when a device or device host configuration takes control of its initialization
        /// through configuration to be able to use <c>IConfigurationSection.Bind()</c> or <c>IConfigurationSection.Get()</c>
        /// provided by the Microsoft.Extensions.Configuration.Binder package without the child before manually handling
        /// the child content.
        /// </para>
        /// </summary>
        /// <param name="this">This configuration section.</param>
        /// <param name="excludedChildName">The child to hide.</param>
        /// <returns>A section where the <paramref name="excludedChildName"/>doesn't appear.</returns>
        public static IConfigurationSection CreateSectionWithout( this IConfigurationSection @this, string excludedChildName )
        {
            return new SkipSectionChildWrapper( @this, excludedChildName );
        }

        sealed class SkipSectionChildWrapper : IConfigurationSection
        {
            sealed class Empty : IConfigurationSection
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

            string _excludedChildName;
            IConfigurationSection _innerSection;

            public SkipSectionChildWrapper( IConfigurationSection innerSection, string excludedChildName )
            {
                Throw.CheckNotNullArgument( innerSection );
                Throw.CheckNotNullOrWhiteSpaceArgument( excludedChildName );
                _innerSection = innerSection;
                _excludedChildName = excludedChildName;
            }

            public string Key => _innerSection.Key;

            public string Path => _innerSection.Path;

            public string? Value
            {
                get => _innerSection.Value;
                set => Throw.NotSupportedException();
            }

            public string ExcludedChildName
            {
                get => _excludedChildName;
                set
                {
                    Throw.CheckNotNullOrWhiteSpaceArgument( value );
                    _excludedChildName = value;
                }
            }

            public IConfigurationSection InnerSection
            {
                get => _innerSection;
                set
                {
                    Throw.CheckNotNullArgument( value );
                    _innerSection = value;
                }
            }

            public string? this[string key]
            {
                get => key == _excludedChildName ? null : _innerSection[key];
                set => Throw.NotSupportedException();
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                return _innerSection.GetChildren().Where( c => !IsExcludedPath( c.Path ) );
            }

            bool IsExcludedPath( string path )
            {
                Debug.Assert( ConfigurationPath.KeyDelimiter == ":" );
                return path.EndsWith( _excludedChildName, StringComparison.OrdinalIgnoreCase )
                        && path[path.Length - _excludedChildName.Length - 1] == ':';
            }

            public IChangeToken GetReloadToken() => InnerSection.GetReloadToken();

            public IConfigurationSection GetSection( string key )
            {
                return key != _excludedChildName ? _innerSection.GetSection( key ) : new Empty( this, key );
            }
        }
    }
}
