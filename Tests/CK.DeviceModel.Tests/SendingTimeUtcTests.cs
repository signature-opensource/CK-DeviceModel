using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class SendingTimeUtcTests
    {
        [Test]
        public void SendingTimeUtc_and_ImmediateSending_are_exclusive()
        {
            var d = DateTime.UtcNow;

            var cmd = new FlashCommand();
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.SendingTimeUtc = d;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().Be( d );

            cmd.ImmediateSending = true;
            cmd.ImmediateSending.Should().BeTrue();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.SendingTimeUtc = d;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().Be( d );

            cmd.SendingTimeUtc = null;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            cmd.ImmediateSending = true;
            cmd.SendingTimeUtc = CK.Core.Util.UtcMinValue;
            cmd.ImmediateSending.Should().BeFalse();
            cmd.SendingTimeUtc.Should().BeNull();

            FluentActions.Invoking( () => cmd.SendingTimeUtc = DateTime.Now ).Should().Throw<ArgumentException>();
        }
    }
}
