using RoguelikeServerMVP.Game;

namespace GameTests;

/// <summary>
/// End-to-end behaviour of a real <see cref="GameEngine"/> run: spawn invariants,
/// turn advancement, wall blocking, seeded reproducibility, restart healing, and
/// room transitions through doors. These exercise the engine the way the MCP server does.
/// </summary>
public class EngineFlowTests
{
    private static GameConfig Config() => new GameConfig
    {
        ViewWidth = 11, ViewHeight = 9,
        RoomWidth = 20, RoomHeight = 20,
        PlayerDefaultMaxHp = 10, PlayerAttackDamage = 3,
        MobAggroRange = 5, AggressiveMobAggroRange = 8,
        BaseRooms = 5, RoomsPerFloor = 2, MaxFloors = 3,
    };

    [Fact]
    public void NewEngine_StartsOnFloorOneInAClearedStartRoom()
    {
        var engine = new GameEngine(Config());
        Assert.Equal(1, engine.Floor.Number);
        Assert.True(engine.Floor.Current.IsStart);
        Assert.True(engine.Floor.Current.Cleared);
        Assert.True(engine.State.Player.IsAlive);
    }

    [Fact]
    public void PlayerMove_IntoOpenInterior_MovesAndAdvancesTurn()
    {
        var engine = new GameEngine(Config());
        var start = engine.State.Player.Position;
        var turn = engine.State.TurnNumber;

        engine.PlayerMove(Direction.Right);

        Assert.Equal(new Position(start.X + 1, start.Y), engine.State.Player.Position);
        Assert.Equal(turn + 1, engine.State.TurnNumber);
    }

    [Fact]
    public void PlayerSkipTurn_AdvancesTurnWithoutMoving()
    {
        var engine = new GameEngine(Config());
        var pos = engine.State.Player.Position;
        var turn = engine.State.TurnNumber;

        engine.PlayerSkipTurn();

        Assert.Equal(pos, engine.State.Player.Position);
        Assert.Equal(turn + 1, engine.State.TurnNumber);
    }

    [Fact]
    public void StartWithSeed_IsReproducible()
    {
        var e1 = new GameEngine(Config());
        var e2 = new GameEngine(Config());
        e1.StartWithSeed(2024);
        e2.StartWithSeed(2024);

        var rooms1 = e1.Floor.AllRooms.Select(r => (r.GridX, r.GridY, r.IsExit)).OrderBy(t => t).ToArray();
        var rooms2 = e2.Floor.AllRooms.Select(r => (r.GridX, r.GridY, r.IsExit)).OrderBy(t => t).ToArray();
        Assert.Equal(rooms1, rooms2);
    }

    [Fact]
    public void Restart_HealsThePlayerToFull()
    {
        var engine = new GameEngine(Config());
        engine.State.Player.TakeDamage(5, engine.State);
        Assert.True(engine.State.Player.Hp < engine.State.Player.MaxHp);

        engine.Restart();

        Assert.Equal(engine.State.Player.MaxHp, engine.State.Player.Hp);
        Assert.Equal(1, engine.Floor.Number);
    }

    [Fact]
    public void PlayerMove_ThroughADoor_ChangesRooms()
    {
        var engine = new GameEngine(Config());
        var startCoords = (engine.Floor.CurrentX, engine.Floor.CurrentY);

        // The start room is cleared, so its doors are open. Walking along a door's axis
        // eventually carries the player into the neighbouring room.
        var dir = engine.Floor.Current.Doors.First();
        for (var i = 0; i < Config().RoomWidth + Config().RoomHeight; i++)
        {
            engine.PlayerMove(dir);
            if ((engine.Floor.CurrentX, engine.Floor.CurrentY) != startCoords)
                break;
        }

        Assert.NotEqual(startCoords, (engine.Floor.CurrentX, engine.Floor.CurrentY));
    }
}
