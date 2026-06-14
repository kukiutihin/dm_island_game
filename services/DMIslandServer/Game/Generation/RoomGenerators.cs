using System;
using System.Collections.Generic;
using System.Linq;

namespace RoguelikeServerMVP.Game.Generation
{
    public enum RoomType
    {
        Start,
        Cave,
        End
    }

    /// <summary>
    /// Produces a walkable grid for a room.
    /// walkable[x, y] == true  => floor (passable)
    /// walkable[x, y] == false => wall (blocked)
    /// </summary>
    public interface IRoomGenerator
    {
        bool[,] Generate(int width, int height, int seed);
    }

    /// <summary>
    /// Cellular-automata cave generator. Seeded and deterministic.
    /// Guarantees: solid border walls, a single connected floor region,
    /// and an open area around the room centre (so the player can spawn).
    /// </summary>
    public class CaveRoomGenerator : IRoomGenerator
    {
        private readonly double _fillProbability;
        private readonly int _smoothingPasses;

        public CaveRoomGenerator(double fillProbability = 0.45, int smoothingPasses = 4)
        {
            _fillProbability = fillProbability;
            _smoothingPasses = smoothingPasses;
        }

        public bool[,] Generate(int width, int height, int seed)
        {
            var rand = new Random(seed);
            var floor = new bool[width, height];

            // 1) Random fill. Border is always wall.
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (IsBorder(x, y, width, height))
                    floor[x, y] = false;
                else
                    floor[x, y] = rand.NextDouble() >= _fillProbability;
            }

            // 2) Smooth with cellular-automata rules.
            for (var pass = 0; pass < _smoothingPasses; pass++)
                floor = SmoothStep(floor, width, height);

            // 3) Carve an open plus at the centre so a spawn always exists.
            CarveCentre(floor, width, height);

            // 4) Keep only the largest connected floor region.
            KeepLargestRegion(floor, width, height);

            return floor;
        }

        private static bool IsBorder(int x, int y, int width, int height)
            => x == 0 || y == 0 || x == width - 1 || y == height - 1;

        private bool[,] SmoothStep(bool[,] floor, int width, int height)
        {
            var next = new bool[width, height];
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (IsBorder(x, y, width, height))
                {
                    next[x, y] = false;
                    continue;
                }

                var wallNeighbours = CountWallNeighbours(floor, x, y, width, height);
                // Standard 4-5 rule: become wall if surrounded by walls.
                next[x, y] = wallNeighbours < 5;
            }

            return next;
        }

        private static int CountWallNeighbours(bool[,] floor, int cx, int cy, int width, int height)
        {
            var count = 0;
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var nx = cx + dx;
                var ny = cy + dy;

                // Out of bounds counts as wall.
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    count++;
                    continue;
                }

                if (!floor[nx, ny]) count++;
            }

            return count;
        }

        private static void CarveCentre(bool[,] floor, int width, int height)
        {
            var cx = width / 2;
            var cy = height / 2;
            int[][] offsets =
            {
                new[] { 0, 0 }, new[] { 1, 0 }, new[] { -1, 0 },
                new[] { 0, 1 }, new[] { 0, -1 }
            };

            foreach (var o in offsets)
            {
                var x = cx + o[0];
                var y = cy + o[1];
                if (x > 0 && y > 0 && x < width - 1 && y < height - 1)
                    floor[x, y] = true;
            }
        }

        private static void KeepLargestRegion(bool[,] floor, int width, int height)
        {
            var visited = new bool[width, height];
            List<(int x, int y)>? largest = null;

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (!floor[x, y] || visited[x, y]) continue;

                var region = FloodFill(floor, visited, x, y, width, height);
                if (largest == null || region.Count > largest.Count)
                    largest = region;
            }

            if (largest == null) return;

            var keep = new HashSet<(int, int)>(largest);
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                if (floor[x, y] && !keep.Contains((x, y)))
                    floor[x, y] = false;
        }

        private static List<(int x, int y)> FloodFill(
            bool[,] floor, bool[,] visited, int startX, int startY, int width, int height)
        {
            var region = new List<(int x, int y)>();
            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));
            visited[startX, startY] = true;

            int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                region.Add((x, y));

                foreach (var d in dirs)
                {
                    var nx = x + d[0];
                    var ny = y + d[1];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    if (visited[nx, ny] || !floor[nx, ny]) continue;
                    visited[nx, ny] = true;
                    stack.Push((nx, ny));
                }
            }

            return region;
        }
    }

    /// <summary>
    /// Helpers to turn a generated walkable grid into the live <see cref="Room"/>
    /// and to place entities on valid floor tiles.
    /// </summary>
    public static class RoomGen
    {
        /// <summary>Applies the walkable grid to a room.</summary>
        public static void ApplyToRoom(Room room, bool[,] walkable)
        {
            for (var x = 0; x < room.Width; x++)
            for (var y = 0; y < room.Height; y++)
                room.SetWalkable(new Position(x, y), walkable[x, y]);
        }

        /// <summary>Returns every wall tile so the caller can spawn Wall entities for rendering.</summary>
        public static IEnumerable<Position> WallPositions(bool[,] walkable)
        {
            var width = walkable.GetLength(0);
            var height = walkable.GetLength(1);
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                if (!walkable[x, y])
                    yield return new Position(x, y);
        }

        /// <summary>
        /// Finds the nearest floor tile to <paramref name="preferred"/> using a
        /// breadth-first ring search. Falls back to the first floor tile found.
        /// </summary>
        public static Position FindNearestWalkable(bool[,] walkable, Position preferred)
        {
            var width = walkable.GetLength(0);
            var height = walkable.GetLength(1);

            bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

            if (InBounds(preferred.X, preferred.Y) && walkable[preferred.X, preferred.Y])
                return preferred;

            var maxRadius = Math.Max(width, height);
            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // ring only
                    var x = preferred.X + dx;
                    var y = preferred.Y + dy;
                    if (InBounds(x, y) && walkable[x, y])
                        return new Position(x, y);
                }
            }

            // Fallback: any floor tile.
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                if (walkable[x, y])
                    return new Position(x, y);

            return preferred;
        }

        /// <summary>
        /// Finds a floor tile close to <paramref name="preferred"/> but at least
        /// <paramref name="minDistanceFromAvoid"/> tiles (Manhattan) away from
        /// <paramref name="avoid"/> — used to keep mobs from spawning on top of
        /// the player. Falls back to the nearest floor tile if none qualifies.
        /// </summary>
        public static Position FindWalkableAwayFrom(
            bool[,] walkable, Position preferred, Position avoid, int minDistanceFromAvoid)
        {
            var width = walkable.GetLength(0);
            var height = walkable.GetLength(1);

            static int Manhattan(int x, int y, Position p) => Math.Abs(x - p.X) + Math.Abs(y - p.Y);

            var hasBest = false;
            var best = preferred;
            var bestDistToPreferred = int.MaxValue;

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (!walkable[x, y]) continue;
                if (Manhattan(x, y, avoid) < minDistanceFromAvoid) continue;

                var d = Manhattan(x, y, preferred);
                if (d < bestDistToPreferred)
                {
                    bestDistToPreferred = d;
                    best = new Position(x, y);
                    hasBest = true;
                }
            }

            return hasBest ? best : FindNearestWalkable(walkable, preferred);
        }
    }
}
