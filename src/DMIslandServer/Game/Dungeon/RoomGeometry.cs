namespace RoguelikeServerMVP.Game.Dungeon;

/// <summary>
/// Geometry helpers for a rectangular room: where doors sit on each wall, and
/// where the player lands when entering a room through a given direction.
/// </summary>
public static class RoomGeometry
{
    /// <summary>Grid offset to the neighbouring room when moving in a direction.</summary>
    public static (int dx, int dy) Delta(Direction dir) => dir switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        Direction.Right => (1, 0),
        _ => (0, 0)
    };

    public static Direction Opposite(Direction dir) => dir switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => dir
    };

    /// <summary>The door tile (on the border) for a given wall.</summary>
    public static Position DoorTile(int width, int height, Direction dir) => dir switch
    {
        Direction.Up => new Position(width / 2, 0),
        Direction.Down => new Position(width / 2, height - 1),
        Direction.Left => new Position(0, height / 2),
        Direction.Right => new Position(width - 1, height / 2),
        _ => new Position(width / 2, height / 2)
    };

    /// <summary>
    /// Where the player stands after walking through a door in direction
    /// <paramref name="movedDir"/>: just inside the opposite wall of the new room.
    /// </summary>
    public static Position EntryTile(int width, int height, Direction movedDir) => movedDir switch
    {
        Direction.Right => new Position(1, height / 2),
        Direction.Left => new Position(width - 2, height / 2),
        Direction.Down => new Position(width / 2, 1),
        Direction.Up => new Position(width / 2, height - 2),
        _ => new Position(width / 2, height / 2)
    };
}
