using Pathfinding.Geometry;
using Xunit;

namespace Pathfinding.Tests.Geometry
{
    public class Vec2Tests
    {
        [Fact]
        public void Constructor_StoresXZ()
        {
            var v = new Vec2(3f, 7f);
            Assert.Equal(3f, v.X);
            Assert.Equal(7f, v.Z);
        }

        [Fact]
        public void DistanceTo_ReturnsEuclidean()
        {
            var a = new Vec2(0f, 0f);
            var b = new Vec2(3f, 4f);
            Assert.Equal(5f, a.DistanceTo(b), precision: 4);
        }

        [Fact]
        public void Add_ReturnsSumComponents()
        {
            var result = new Vec2(1f, 2f) + new Vec2(3f, 4f);
            Assert.Equal(4f, result.X);
            Assert.Equal(6f, result.Z);
        }

        [Fact]
        public void Subtract_ReturnsDiffComponents()
        {
            var result = new Vec2(5f, 7f) - new Vec2(2f, 3f);
            Assert.Equal(3f, result.X);
            Assert.Equal(4f, result.Z);
        }

        [Fact]
        public void Multiply_ScalesComponents()
        {
            var result = new Vec2(2f, 3f) * 2f;
            Assert.Equal(4f, result.X);
            Assert.Equal(6f, result.Z);
        }
    }
}
