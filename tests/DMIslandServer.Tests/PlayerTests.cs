using Xunit;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game;

namespace GameTests;

public class PlayerTests
{
    [Fact]
    public void Player_Creation_ShouldInitializeProperties()
    {
        var player = new Player(new Position(5, 10), maxHp: 10);

        Assert.Equal(5, player.Position.X);
        Assert.Equal(10, player.Position.Y);
        Assert.Equal(10, player.MaxHp);
        Assert.Equal(10, player.Hp);
        Assert.True(player.IsAlive);
        Assert.Equal(EntityType.Player, player.Type);
    }

    [Fact]
    public void Player_TryMove_OnWalkableTile_UpdatesPosition()
    {
        var player = new Player(new Position(5, 5), 10);
        var room = new Room(10, 10);
        var state = new GameState(player, room);

        player.TryMove(Direction.Right, state);

        Assert.Equal(6, player.Position.X);
        Assert.Equal(5, player.Position.Y);
    }

    [Fact]
    public void Player_TryMove_IntoWall_DoesNotMove()
    {
        var player = new Player(new Position(5, 5), 10);
        var room = new Room(10, 10);
        room.SetWalkable(new Position(6, 5), false);
        var state = new GameState(player, room);

        player.TryMove(Direction.Right, state);

        Assert.Equal(5, player.Position.X);
        Assert.Equal(5, player.Position.Y);
    }
}
