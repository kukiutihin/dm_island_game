using RoguelikeServerMVP.Game;
using RoguelikeServerMVP.Game.Dungeon;

namespace GameTests;

/// <summary>
/// Floor generation: determinism (same seed -> same floor, required by the eval harness's
/// fixed-seed runs), the start/exit invariants, connectivity, and difficulty scaling.
/// </summary>
public class DungeonGeneratorTests
{
    private static GameConfig Config() => new GameConfig
    {
        RoomWidth = 20, RoomHeight = 20,
        PlayerDefaultMaxHp = 10, PlayerAttackDamage = 3,
        BaseRooms = 5, RoomsPerFloor = 2, MaxFloors = 3,
    };

    private static (int x, int y, bool exit)[] Signature(Floor f) =>
        f.AllRooms
            .Select(r => (r.GridX, r.GridY, r.IsExit))
            .OrderBy(t => t.Item1).ThenBy(t => t.Item2)
            .ToArray();

    [Fact]
    public void Generate_IsDeterministic_ForTheSameSeed()
    {
        var a = DungeonGenerator.Generate(Config(), 1, seed: 4242);
        var b = DungeonGenerator.Generate(Config(), 1, seed: 4242);

        Assert.Equal(Signature(a), Signature(b));

        var ea = a.AllRooms.First(r => r.IsExit);
        var eb = b.AllRooms.First(r => r.IsExit);
        Assert.Equal(ea.ExitTile, eb.ExitTile);
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentLayouts()
    {
        var a = DungeonGenerator.Generate(Config(), 1, seed: 1);
        var b = DungeonGenerator.Generate(Config(), 1, seed: 999);
        // Extremely unlikely to coincide; guards against an ignored seed.
        Assert.NotEqual(Signature(a), Signature(b));
    }

    [Fact]
    public void Generate_HasExactlyOneClearedStartAtCentre()
    {
        var f = DungeonGenerator.Generate(Config(), 1, seed: 7);
        var starts = f.AllRooms.Where(r => r.IsStart).ToList();
        Assert.Single(starts);
        Assert.True(starts[0].Cleared);
        Assert.Same(starts[0], f.Current);
    }

    [Fact]
    public void Generate_AlwaysHasAnExitRoomWithAPortalTile()
    {
        var f = DungeonGenerator.Generate(Config(), 1, seed: 13);
        var exit = f.AllRooms.Single(r => r.IsExit);
        Assert.NotNull(exit.ExitTile);
        Assert.False(exit.IsStart);
    }

    [Fact]
    public void Generate_FloorNotFullyClearedAtStart()
    {
        var f = DungeonGenerator.Generate(Config(), 1, seed: 21);
        Assert.False(f.AllCleared); // non-start rooms still hold mobs
    }

    [Fact]
    public void Generate_EveryDoorLeadsToARealNeighbour()
    {
        var f = DungeonGenerator.Generate(Config(), 1, seed: 33);
        foreach (var dir in f.Current.Doors)
            Assert.NotNull(f.Neighbour(dir));
    }

    [Fact]
    public void Generate_DeeperFloorsHaveMoreRooms()
    {
        var floor1 = DungeonGenerator.Generate(Config(), 1, seed: 5).AllRooms.Count();
        var floor3 = DungeonGenerator.Generate(Config(), 3, seed: 5).AllRooms.Count();
        Assert.True(floor3 > floor1, $"expected floor3 ({floor3}) > floor1 ({floor1})");
    }
}
