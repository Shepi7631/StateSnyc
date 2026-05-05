namespace Pathfinding.Data
{
    public readonly struct Portal
    {
        public readonly Vec2 Left;
        public readonly Vec2 Right;

        public Portal(Vec2 left, Vec2 right) { Left = left; Right = right; }
    }
}
