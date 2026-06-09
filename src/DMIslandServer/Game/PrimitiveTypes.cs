using Microsoft.VisualBasic.CompilerServices;

namespace RoguelikeServerMVP.Game
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public struct Position(int x, int y) : IEquatable<Position>
    {
        public int X { get; } = x;
        public int Y { get; } = y;

        public Position Move(Direction dir)
        {
            return dir switch
            {
                Direction.Up    => new Position(X, Y - 1),
                Direction.Down  => new Position(X, Y + 1),
                Direction.Left  => new Position(X - 1, Y),
                Direction.Right => new Position(X + 1, Y),
                _               => this
            };
        }

        public bool Equals(Position other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Position p && Equals(p);

        public static bool operator ==(Position self, Position other)
        {
            return self.Equals(other);
        }

        public static bool operator !=(Position self, Position other)
        {
            return !(self == other);
        }

        public static Position operator +(Position self, Position other)
        {
            return new Position(self.X + other.X, self.Y + other.Y);
        }

        public int SquaredDistanceTo(Position other)
        {
            return (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
        }

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X},{Y})";
        
        public static Position Zero => new Position(0, 0);

        public static IEnumerable<Position> CreateRectangle(Position first, Position second)
        {
            var bottom = int.Min(first.Y, second.Y);
            var top = int.Max(first.Y, second.Y);
            var left = int.Min(first.X, second.X);
            var right = int.Max(first.X, second.X);
            for (var x = left; x <= right; x++)
            for (var y = bottom; y <= top; y++)
                yield return new Position(x, y);
        }
    }
}