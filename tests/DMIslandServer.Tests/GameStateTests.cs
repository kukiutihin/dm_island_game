using Xunit;
using RoguelikeServerMVP.Game;
using RoguelikeServerMVP.Game.Entities.Factory.Preset;

namespace GameTests;

public class GameStateTests
{
    private static GameState CreateState(int width = 10, int height = 10)
    {
        var player = new Player(new Position(0, 0), 10, 3);
        var room = new Room(width, height);
        return new GameState(player, room);
    }

    [Fact]
    public void CanMoveTo_WalkableEmptyTile_ReturnsTrue()
    {
        var state = CreateState();
        Assert.True(state.CanMoveTo(new Position(5, 5)));
    }

    [Fact]
    public void CanMoveTo_OutsideRoom_ReturnsFalse()
    {
        var state = CreateState();
        Assert.False(state.CanMoveTo(new Position(-1, 0)));
        Assert.False(state.CanMoveTo(new Position(100, 100)));
    }

    [Fact]
    public void CanMoveTo_BlockedByWall_ReturnsFalse()
    {
        var state = CreateState();
        state.AddObject(new Wall(new Position(3, 3)));
        Assert.False(state.CanMoveTo(new Position(3, 3)));
    }

    [Fact]
    public void GetMobAt_ReturnsAddedMob()
    {
        var state = CreateState();
        var mob = new ModusPonens(new Position(4, 4));
        state.AddMob(mob);

        var found = state.GetMobAt(new Position(4, 4));
        Assert.Same(mob, found);
    }

    [Fact]
    public void AddObject_AddsWallToStaticObjects()
    {
        var state = CreateState();
        state.AddObject(new Wall(new Position(1, 1)));

        Assert.Contains(state.StaticObjects,
            o => o is Wall && o.Position == new Position(1, 1));
    }
}
