using System;
using System.Threading;

namespace CK.DeviceModel.Tests;

class PhysicalMachine : IDisposable
{
    readonly Timer _timer;
    readonly Random _random;
    Action<int>? _measure;


    public PhysicalMachine( int delay, Action<int> m, bool alwaysPositiveMeasure )
    {
        _measure = m;
        _random = new Random( 3712 );
        if( alwaysPositiveMeasure )
            _timer = new Timer( _ => _measure?.Invoke( _random.Next( 10 ) + 1 ), null, 0, delay );
        else _timer = new Timer( _ => _measure?.Invoke( _random.Next( 10 ) - 1 ), null, 0, delay );
    }

    public void Dispose()
    {
        _measure = null;
        _timer.Dispose();
    }

}
