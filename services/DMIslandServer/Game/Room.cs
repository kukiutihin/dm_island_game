namespace RoguelikeServerMVP.Game
{
    public class Room
    {
        public Guid Id { get; set; }
        public int Width { get; }
        public int Height { get; }

        private readonly bool[,] _walkable;

        public Room(int width, int height)
        {
            Id = Guid.NewGuid();
            Width = width;
            Height = height;
            _walkable = new bool[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _walkable[x, y] = true;
        }

        public bool IsInside(Position pos)
        {
            return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
        }

        public bool IsWalkable(Position pos)
        {
            return IsInside(pos) && _walkable[pos.X, pos.Y];
        }

        public void SetWalkable(Position pos, bool isWalkable)
        {
            if (!IsInside(pos)) return;
            _walkable[pos.X, pos.Y] = isWalkable;
        }
    }
}