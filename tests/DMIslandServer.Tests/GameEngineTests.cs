using Xunit;
using RoguelikeServerMVP;
using RoguelikeServerMVP.Game;

namespace GameTests;

public class GameEngineTests
{
    private static GameConfig CreateConfig() => new GameConfig
    {
        ViewWidth = 11,
        ViewHeight = 9,
        RoomWidth = 20,
        RoomHeight = 20,
        PlayerDefaultMaxHp = 10,
        PlayerAttackDamage = 3,
        MobAggroRange = 5,
        AggressiveMobAggroRange = 8
    };

    private static (GameEngine engine, Player player, GameState state) CreateGame(int px, int py)
    {
        var config = CreateConfig();
        var player = new Player(new Position(px, py), config.PlayerDefaultMaxHp);
        var room = new Room(10, 10);
        var state = new GameState(player, room);
        return (new GameEngine(config), player, state);
    }

    [Fact]
    public void PlayerMove_ValidMove_UpdatesPositionAndAdvancesTurn()
    {
        var (engine, player, state) = CreateGame(1, 1);
        var turn = state.TurnNumber;

        engine.PlayerMove(Direction.Right);

        Assert.Equal(2, player.Position.X);
        Assert.Equal(1, player.Position.Y);
        Assert.Equal(turn + 1, state.TurnNumber);
    }

    [Fact]
    public void PlayerMove_BlockedByWall_DoesNotMove()
    {
        var (engine, player, state) = CreateGame(1, 1);
        state.AddObject(new Wall(new Position(2, 1)));

        engine.PlayerMove(Direction.Right);

        Assert.Equal(1, player.Position.X);
        Assert.Equal(1, player.Position.Y);
    }

    [Fact]
    public void PlayerSkipTurn_IncrementsTurn()
    {
        var (engine, _, state) = CreateGame(0, 0);
        var turn = state.TurnNumber;

        engine.PlayerSkipTurn();

        Assert.Equal(turn + 1, state.TurnNumber);
    }

    [Fact]
    public void PlayerAttack_AdvancesTurnAndPlayerSurvives()
    {
        var (engine, player, state) = CreateGame(5, 5);
        var turn = state.TurnNumber;

        engine.PlayerAttack(Direction.Right);

        Assert.Equal(turn + 1, state.TurnNumber);
        Assert.True(player.IsAlive);
    }
}
