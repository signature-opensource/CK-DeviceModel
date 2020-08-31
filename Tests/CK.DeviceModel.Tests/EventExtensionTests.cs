using NUnit.Framework;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace CK.DeviceModel.Tests
{
    [TestFixture]
    public class EventExtensionTests
    {
        public struct YMCA
        {
            public double Y;
            public double M;
            public double C;
            public double A;

            public YMCA(double cury, double curm, double curc, double cura)
            {
                Y = cury;
                M = curm;
                C = curc;
                A = cura;
            }
        }

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
            e.MarshalToFloatArray(dest, 20).Should().BeFalse();
        }

        [Test]
        public void MarshallingStructToEventAndOtherWayShouldWork()
        {
            double y = 123.129, m = 9318381.238, c = 0, a = -923.12;

            YMCA testStruct = new YMCA(y,m,c,a);
            Event e = testStruct.ToEvent(93);
            YMCA remarshalled = default;
            remarshalled.Y.Should().Be(0);
            e.MarshalToStruct(ref remarshalled).Should().BeTrue();
            remarshalled.Y.Should().Be(y);
            remarshalled.M.Should().Be(m);
            remarshalled.C.Should().Be(c);
            remarshalled.A.Should().Be(a);
        }

        [Test]
        public void MarshallingStructArrayToEventBackAndForthShouldWork()
        {
            double y = 123.129, m = 9318381.238, c = 0, a = -923.12;

            YMCA basetest = new YMCA(y, m, c, a);

            List<YMCA> listTest = new List<YMCA>() { basetest };
            for (int i = 0; i < 10; i++)
            {
                listTest.Add(new YMCA(listTest.Last().Y / 2.0, listTest.Last().M + 23, listTest.Last().C - 2.9239, listTest.Last().A * 3.0));
            }

            Event e = listTest.ToEvent(57);
            e.EventCode.Should().Be(57);

            YMCA[] marshalled = e.MarshalStructArray<YMCA>();

            marshalled.Should().NotBeNull();
            marshalled.Should().NotBeEmpty();
            marshalled.Length.Should().Be(listTest.Count);
            for (int i = 0; i < listTest.Count; i++)
            {
                marshalled[i].Y.Should().Be(listTest[i].Y);
                marshalled[i].M.Should().Be(listTest[i].M);
                marshalled[i].C.Should().Be(listTest[i].C);
                marshalled[i].A.Should().Be(listTest[i].A);
            }
        }
    }
}
