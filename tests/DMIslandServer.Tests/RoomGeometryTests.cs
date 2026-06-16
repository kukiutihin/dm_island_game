using RoguelikeServerMVP.Game;
using RoguelikeServerMVP.Game.Dungeon;

namespace GameTests;

/// <summary>
/// Door/entry geometry of a rectangular room. Wrong door tiles would let the agent walk
/// "through" a wall or land outside the room on a transition, so these are pinned exactly.
/// </summary>
public class RoomGeometryTests
{
    [Theory]
    [InlineData(Direction.Up, 0, -1)]
    [InlineData(Direction.Down, 0, 1)]
    [InlineData(Direction.Left, -1, 0)]
    [InlineData(Direction.Right, 1, 0)]
    public void Delta_MatchesGridDirection(Direction dir, int dx, int dy)
    {
        Assert.Equal((dx, dy), RoomGeometry.Delta(dir));
    }

    [Theory]
    [InlineData(Direction.Up, Direction.Down)]
    [InlineData(Direction.Left, Direction.Right)]
    public void Opposite_FlipsDirection(Direction a, Direction b)
    {
        Assert.Equal(b, RoomGeometry.Opposite(a));
        Assert.Equal(a, RoomGeometry.Opposite(b));
    }

    [Fact]
    public void DoorTile_SitsOnTheMiddleOfEachWall()
    {
        Assert.Equal(new Position(10, 0), RoomGeometry.DoorTile(20, 20, Direction.Up));
        Assert.Equal(new Position(10, 19), RoomGeometry.DoorTile(20, 20, Direction.Down));
        Assert.Equal(new Position(0, 10), RoomGeometry.DoorTile(20, 20, Direction.Left));
        Assert.Equal(new Position(19, 10), RoomGeometry.DoorTile(20, 20, Direction.Right));
    }

    [Fact]
    public void EntryTile_LandsJustInsideTheOppositeWall()
    {
        // Walking RIGHT into a room lands one tile inside its LEFT wall.
        Assert.Equal(new Position(1, 10), RoomGeometry.EntryTile(20, 20, Direction.Right));
        Assert.Equal(new Position(18, 10), RoomGeometry.EntryTile(20, 20, Direction.Left));
        Assert.Equal(new Position(10, 1), RoomGeometry.EntryTile(20, 20, Direction.Down));
        Assert.Equal(new Position(10, 18), RoomGeometry.EntryTile(20, 20, Direction.Up));
    }
}
