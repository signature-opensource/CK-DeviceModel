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

    }
}
