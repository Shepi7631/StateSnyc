using System;

namespace Pathfinding.Geometry
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Z;

        public Vec2(float x, float z) { X = x; Z = z; }

        public float DistanceTo(Vec2 other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 v, float s) => new Vec2(v.X * s, v.Z * s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public bool Equals(Vec2 other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X}, {Z})";
    }
}
