using CK.Core;
using CK.Testing;
using FluentAssertions;
using FluentAssertions.Equivalency;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public partial class AsyncLockTests
    {
        [Test]
        public async Task SemaphoreSlim_deadlocks_when_reentering()
        {
            static async Task Deadlock()
            {
                var s = new SemaphoreSlim( 1, 1 );
                await s.WaitAsync();
                await s.WaitAsync();
                throw new Exception( "Never here..." );
            }

            var dead = Deadlock();
            var timeout = Task.Delay( 100 );
            await Task.WhenAny( dead, timeout );

            timeout.IsCompletedSuccessfully.Should().BeTrue();
            dead.IsCompleted.Should().BeFalse();
        }

        [TestCase( true, true, true )]
        [TestCase( true, true, false )]
        [TestCase( true, false, true )]
        [TestCase( true, false, false )]
        [TestCase( false, true, true )]
        [TestCase( false, true, false )]
        [TestCase( false, false, true )]
        [TestCase( false, false, false )]
        public async Task our_AsyncLock_handles_reentrancy(bool firstAsync, bool secondAsync, bool thirdAsync )
        {
            var m = TestHelper.Monitor;

            var l = new AsyncLock( LockRecursionPolicy.SupportsRecursion );

            l.IsEnteredBy( m ).Should().BeFalse();

            if( firstAsync ) await l.EnterAsync( m );
            else l.Enter( m );

            l.IsEnteredBy( m ).Should().BeTrue();

            if( secondAsync ) await l.EnterAsync( m );
            else l.Enter( m );

            l.IsEnteredBy( m ).Should().BeTrue();

            if( thirdAsync ) await l.EnterAsync( m );
            else l.Enter( m );

            using( await l.LockAsync( m ) )
            {
                l.IsEnteredBy( m ).Should().BeTrue();
            }

            l.IsEnteredBy( m ).Should().BeTrue();

            l.Leave( m );
            l.IsEnteredBy( m ).Should().BeTrue();

            l.Leave( m );
            l.IsEnteredBy( m ).Should().BeTrue();

            l.Leave( m );
            l.IsEnteredBy( m ).Should().BeFalse();

            using( await l.LockAsync( m ) )
            {
                l.IsEnteredBy( m ).Should().BeTrue();
            }

            using( l.Lock( m ) )
            {
                l.IsEnteredBy( m ).Should().BeTrue();
            }

        }

        [TestCase( 3000, 3000 )]
        [TestCase( 35000, 25000 )]
        public async Task simple_stress_test( int syncIncLoop, int asyncDecLoop )
        {
            IActivityMonitor m1 = new ActivityMonitor( applyAutoConfigurations: false );
            IActivityMonitor m2 = new ActivityMonitor( applyAutoConfigurations: false );

            AsyncLock guard = new AsyncLock( LockRecursionPolicy.SupportsRecursion, "G" );

            int nByM1 = 0;
            int nByM2 = 0;

            Action job = () =>
            {
                Thread.Sleep( 10 );
                for( int i = 0; i < syncIncLoop; i++ )
                {
                    guard.Enter( m2 );
                    nByM2++;
                    guard.Leave( m2 );

                    guard.Enter( m1 );
                    nByM1++;
                    guard.Leave( m1 );
                }
            };

            Func<Task> asyncJob = async () =>
            {
                Thread.Sleep( 10 );
                for( int i = 0; i < asyncDecLoop; i++ )
                {
                    await guard.EnterAsync( m2 );
                    nByM2++;
                    guard.Leave( m2 );

                    await guard.EnterAsync( m1 );
                    nByM1--;
                    guard.Leave( m1 );
                }
            };

            await Task.WhenAll( Task.Run( job ), Task.Run( job ), Task.Run( asyncJob ), Task.Run( asyncJob ) );

            nByM1.Should().Be( syncIncLoop * 2 - asyncDecLoop * 2 );
            nByM2.Should().Be( syncIncLoop * 2 + asyncDecLoop * 2 );
        }
    }
}
