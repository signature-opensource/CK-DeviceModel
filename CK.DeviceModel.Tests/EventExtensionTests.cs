using NUnit.Framework;
using FluentAssertions;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class EventExtensionTests
    {


        [Test]
        public void MarhsallingShouldReturnNullIfNegativeFirstField()
        {
            Event e = default;
            e.Field1.Int0 = -12399;
            float[] dest = e.MarshalToFloatArray(10);
            dest.Should().BeNull();
        }

        [Test]
        public void MarshallingInPlaceShouldeturnNullIfNegativeFirstField()
        {
            Event e = default;
            e.Field1.Int0 = -1299931;
            float[] dest = new float[20];
            e.MarshalToFloatArray(dest, 20);
            dest.Should().BeNull();
        }
    }
}
