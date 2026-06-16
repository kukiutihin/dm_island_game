using Xunit;
using RoguelikeServerMVP.Game;

namespace GameTests;

/// <summary>
/// Player actions driven through the real GameEngine. The engine owns its own GameState
/// (built in its constructor), so these tests act on <c>engine.State</c> rather than a
/// hand-built state — that's the bug the earlier version of these tests had.
/// </summary>
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
        AggressiveMobAggroRange = 8,
        BaseRooms = 5,
        RoomsPerFloor = 2,
        MaxFloors = 3,
    };

    [Fact]
    public void PlayerMove_ValidMove_UpdatesPositionAndAdvancesTurn()
    {
        var engine = new GameEngine(CreateConfig());
        var start = engine.State.Player.Position;
        var turn = engine.State.TurnNumber;

        engine.PlayerMove(Direction.Right);

        Assert.Equal(new Position(start.X + 1, start.Y), engine.State.Player.Position);
        Assert.Equal(turn + 1, engine.State.TurnNumber);
    }

    [Fact]
    public void PlayerMove_BlockedByWall_DoesNotMove()
    {
        var engine = new GameEngine(CreateConfig());

        // Step off the door row, then walk into the left border wall. The player stops just
        // inside the wall (x == 1) and a further push doesn't move them.
        engine.PlayerMove(Direction.Up);
        engine.PlayerMove(Direction.Up);
        for (var i = 0; i < CreateConfig().RoomWidth; i++)
        {
            var before = engine.State.Player.Position;
            engine.PlayerMove(Direction.Left);
            if (engine.State.Player.Position.Equals(before))
                break;
        }

        var atWall = engine.State.Player.Position;
        engine.PlayerMove(Direction.Left);

        Assert.Equal(atWall, engine.State.Player.Position);
        Assert.Equal(1, atWall.X);
    }

    [Fact]
    public void PlayerSkipTurn_IncrementsTurn()
    {
        var engine = new GameEngine(CreateConfig());
        var turn = engine.State.TurnNumber;

        engine.PlayerSkipTurn();

        Assert.Equal(turn + 1, engine.State.TurnNumber);
    }

    [Fact]
    public void PlayerAttack_AdvancesTurnAndPlayerSurvives()
    {
        var engine = new GameEngine(CreateConfig());
        var turn = engine.State.TurnNumber;

        engine.PlayerAttack(Direction.Right);

        Assert.Equal(turn + 1, engine.State.TurnNumber);
        Assert.True(engine.State.Player.IsAlive);
    }
}
