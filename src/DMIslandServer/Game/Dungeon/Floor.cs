using System.Collections.Generic;
using System.Linq;

namespace RoguelikeServerMVP.Game.Dungeon;

/// <summary>
/// One dungeon floor: a grid of rooms (sparse — many cells are empty) with a
/// current room pointer and a floor number.
/// </summary>
public class Floor(int number, DungeonRoom?[,] rooms, int startX, int startY)
{
    public int Number => number;
    public DungeonRoom?[,] Rooms => rooms;

    public int GridWidth => rooms.GetLength(0);
    public int GridHeight => rooms.GetLength(1);

    public int CurrentX { get; set; } = startX;
    public int CurrentY { get; set; } = startY;

    public DungeonRoom Current => rooms[CurrentX, CurrentY]!;

    public IEnumerable<DungeonRoom> AllRooms
    {
        get
        {
            foreach (var r in rooms)
                if (r != null)
                    yield return r;
        }
    }

    public bool AllCleared => AllRooms.All(r => r.Cleared);

    /// <summary>The room adjacent to the current room in the given direction, if any.</summary>
    public DungeonRoom? Neighbour(Direction dir)
    {
        var (dx, dy) = RoomGeometry.Delta(dir);
        var nx = CurrentX + dx;
        var ny = CurrentY + dy;
        if (nx < 0 || ny < 0 || nx >= GridWidth || ny >= GridHeight) return null;
        return rooms[nx, ny];
    }
}
