using Microsoft.Extensions.Configuration;
using System.Linq;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace CK.DeviceModel.Configuration.Tests;

readonly struct DynamicConfiguration
{
    public readonly IConfigurationRoot Root;

    public readonly DynamicConfigurationProvider Provider;

    DynamicConfiguration( IConfigurationRoot r )
    {
        Root = r;
        Provider = r.Providers.OfType<DynamicConfigurationProvider>().Single();
    }

    public static DynamicConfiguration Create() => new DynamicConfiguration( new ConfigurationBuilder().Add( new DynamicConfigurationSource() ).Build() );
}

class DynamicConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build( IConfigurationBuilder builder ) => new DynamicConfigurationProvider();
}

class DynamicConfigurationProvider : ConfigurationProvider
{
    public void Remove( string path )
    {
        var keys = Data.Keys.Where( k => k == path || k.Length > path.Length && k.StartsWith( path ) && k[path.Length] == ConfigurationPath.KeyDelimiter[0] ).ToList();
        foreach( var k in keys ) Data.Remove( k );
    }

    public void RaiseChanged() => OnReload();
}
