using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Core
{
    /// <summary>
    /// Immutable capture of a <see cref="IConfigurationSection"/>.
    /// </summary>
    /// <remarks>
    /// If persistence of configuration must be implemented once, the potential parent configurations MUST NOT be flattened
    /// (even the parent for a child): configurations may be combined by consumer in a complex way, the whole parent chain
    /// must be restored to guaranty the same "final configuration".
    /// If you doubt consider this question: how do you know if a given property is "additive" - because it belongs to
    /// a set - or is "overridable"/"replacable"?
    /// </remarks>
    public sealed class ImmutableConfigurationSection : IConfigurationSection
    {
        readonly string _key;
        readonly string _path;
        readonly string? _value;
        readonly ImmutableConfigurationSection[] _children;
        readonly ImmutableConfigurationSection? _lookupParent;

        /// <summary>
        /// Initializes a new <see cref="ImmutableConfigurationSection"/>.
        /// <para>
        /// The <paramref name="lookupParent"/> is used only for <see cref="TryLookupSection(string)"/> and <see cref="TryLookupValue(string)"/>.
        /// It is not exposed and this is intended since it will introduce an inconsistency: this "child" cannot appear in the
        /// parent children (<see cref="ImmutableConfigurationSection.GetChildren()"/>). The new section can even "hide" an existing section of the
        /// parent. Think to it as a convenient fallback mechanism that makes sense from the child section only even if <see cref="LookupAllSection"/>
        /// enables a more complex analysis through the parents: parent sections can be combined in a complex way if needed.
        /// </para>
        /// </summary>
        /// <param name="section">The section to capture.</param>
        /// <param name="lookupParent">
        /// Optional parent of the <paramref name="section"/>. The parent path must match the parent of the
        /// section path otherwise an <see cref="ArgumentException"/> is raised.
        /// </param>
        public ImmutableConfigurationSection( IConfigurationSection section, ImmutableConfigurationSection? lookupParent = null )
        {
            Debug.Assert( ConfigurationPath.KeyDelimiter == ":" );
            if( lookupParent != null
                && (lookupParent.Path.Length != section.Path.Length - section.Key.Length - 1
                    || !section.Path.AsSpan( 0, lookupParent.Path.Length ).Equals( lookupParent.Path, StringComparison.OrdinalIgnoreCase ) ) )
            {
                Throw.ArgumentException( nameof(lookupParent), $"Expected section path to be '{lookupParent.Path}:{section.Key}', got '{section.Path}'." );
            }
            _lookupParent = lookupParent;
            _key = section.Key;
            _path = section.Path;
            _value = section.Value;
            _children = section.GetChildren().Select( c => new ImmutableConfigurationSection( this, c ) ).ToArray();
        }

        // No check for parent.
        ImmutableConfigurationSection( ImmutableConfigurationSection? parent, IConfigurationSection section )
            : this( section )
        {
            _lookupParent = parent;
        }

        // Unexisting section.
        ImmutableConfigurationSection( ImmutableConfigurationSection parent, string path, string key )
        {
            _key = key;
            _path = path;
            _children = Array.Empty<ImmutableConfigurationSection>();
            _lookupParent = parent;
        }

        /// <summary>
        /// Gets a configuration value. Setting it throws a <see cref="NotSupportedException"/>. 
        /// </summary>
        /// <param name="key">The configuration key to find.</param>
        /// <returns>The value or null if not found.</returns>
        public string? this[string key]
        {
            get
            {
                var sKey = key.AsSpan();
                return Find( ref sKey, _children )?.Value;
            }
            set => Throw.NotSupportedException( $"This configuration '{_path}' is locked." );
        }

        /// <inheritdoc />
        public string Key => _key;

        /// <inheritdoc />
        public string Path => _path;

        /// <summary>
        /// Gets the section value. Setting it throws a <see cref="NotSupportedException"/>. 
        /// </summary>
        public string? Value
        {
            get => _value;
            set => Throw.NotSupportedException( $"This configuration '{_path}' is locked." );
        }

        IEnumerable<IConfigurationSection> IConfiguration.GetChildren() => _children;

        /// <summary>
        /// Gets the immediate descendant configuration sub-sections: they are also <see cref="ImmutableConfigurationSection"/>.
        /// </summary>
        /// <returns>The configuration sub-sections.</returns>
        public IReadOnlyList<ImmutableConfigurationSection> GetChildren() => _children;

        IConfigurationSection IConfiguration.GetSection( string key ) => GetSection( key );

        /// <summary>
        /// The standard <see cref="IConfiguration.GetSection(string)"/> creates a non existing section
        /// instance. This one simply return null if the section cannot be found.
        /// </summary>
        /// <param name="key">The key of the configuration section.</param>
        /// <returns>The section or null.</returns>
        public ImmutableConfigurationSection? TryGetSection( string key )
        {
            var sKey = key.AsSpan();
            return Find( ref sKey, _children );
        }

        /// <summary>
        /// Tries to find a value in this section or in the parent section.
        /// If a section with the key is found above but has no value (because it has children),
        /// this returns null. Use <see cref="TryLookupSection(string)"/> to lookup for a section.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The non null value if found.</returns>
        public string? TryLookupValue( string key ) => TryLookupSection( key )?.Value;

        /// <summary>
        /// Tries to find a section in this section or in the parent section.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The non null section if found.</returns>
        public ImmutableConfigurationSection? TryLookupSection( string key )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( key );
            ImmutableConfigurationSection? result;
            var s = this;
            do
            {
                if( (result = s.TryGetSection( key )) != null ) break;
            }
            while( (s = s._lookupParent) != null );
            return result;
        }

        /// <summary>
        /// Enumerates all child sections from root configuration up to the child of this section.
        /// <para>
        /// Sections that appear in the path from the root to this one are skipped by this function.
        /// </para>
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="skipSectionsOnThisPath">
        /// Set it to false to return sections that occur on this path.
        /// This is generally not what you want.
        /// </param>
        /// <returns>All the sections above and the child section if any.</returns>
        public IEnumerable<ImmutableConfigurationSection> LookupAllSection( string key, bool skipSectionsOnThisPath = true ) => DoLookupAllSection( key, skipSectionsOnThisPath ? this : null );

        IEnumerable<ImmutableConfigurationSection> DoLookupAllSection( string key, ImmutableConfigurationSection? caller )
        {
            if( _lookupParent != null )
            {
                foreach( var s in _lookupParent.DoLookupAllSection( key, caller != null ? this : null ) )
                {
                    yield return s;
                }
            }
            var sKey = key.AsSpan();
            var sub = Find( ref sKey, _children, caller );
            if( sub != null ) yield return sub;
        }

        /// <inheritdoc cref="IConfiguration.GetSection(string)"/>
        /// <remarks>
        /// This returns an empty section. Use <see cref="TryGetSection(string)"/>
        /// to have a null section if it doesn't exist.
        /// </remarks>
        public ImmutableConfigurationSection GetSection( string key )
        {
            var sKey = key.AsSpan();
            var s = Find( ref sKey, _children );
            if( s != null ) return s;
            // This mimics the key returned by the standard .Net implementation.
            var errorKey = key;
            if( sKey.Length != errorKey.Length )
            {
                var sErrorKey = sKey.Trim( ':' );
                if( sErrorKey.Contains(':' ) )
                {
                    errorKey = string.Empty;
                }
                else
                {
                    errorKey = sErrorKey.ToString();
                }
            }
            return new ImmutableConfigurationSection( this, ConfigurationPath.Combine( _path, key ), errorKey );
        }

        static ImmutableConfigurationSection? Find( ref ReadOnlySpan<char> sKey, ImmutableConfigurationSection[] children, ImmutableConfigurationSection? skip = null )
        {
            for( ; ; )
            {
                var idx = sKey.IndexOf( ':' );
                if( idx < 0 ) return FindCore( sKey, children, skip );
                var sub = FindCore( sKey.Slice( 0, idx ), children, skip );
                sKey = sKey.Slice( idx + 1 );
                if( sub == null ) return null;
                children = sub._children;
            }

            static ImmutableConfigurationSection? FindCore( ReadOnlySpan<char> sKey, ImmutableConfigurationSection[] children, ImmutableConfigurationSection? skip )
            {
                foreach( var child in children )
                {
                    if( child != skip && sKey.Equals( child.Key, StringComparison.OrdinalIgnoreCase ) ) return child;
                }
                return null;
            }
        }

        /// <summary>
        /// Always returns a never changing token.
        /// </summary>
        /// <returns>A never changing token.</returns>
        public IChangeToken GetReloadToken() => Microsoft.Extensions.FileProviders.NullChangeToken.Singleton;

        /// <summary>
        /// Overridden to display the path and the value or the count of children.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{_path} = {(_value ?? (_children.Length != 0 ? $"{_children.Length} children" : "!Exists"))}";
    }
}
