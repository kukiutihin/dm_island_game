using System;
using System.Linq;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Dungeon;
using RoguelikeServerMVP.Game.Entities;
using RoguelikeServerMVP.Game.Mobs.Factory.Preset;

namespace RoguelikeServerMVP.Game;

public class GameEngine
{
    public GameState State { get; }
    public GameConfig Config { get; }
    public Floor Floor { get; private set; }

    public GameEngine(GameConfig config)
    {
        Config = config;

        var player = new Player(
            new Position(config.RoomWidth / 2, config.RoomHeight / 2),
            config.PlayerDefaultMaxHp,
            config.PlayerAttackDamage);

        State = new GameState(player, new Room(config.RoomWidth, config.RoomHeight));

        StartNewRun(1);
    }

    /// <summary>Begins a brand-new run from floor 1 with a healed player (used on restart).</summary>
    public void Restart()
    {
        State.Player.RestoreFullHealth();
        StartNewRun(1);
    }

    private void StartNewRun(int floorNumber)
    {
        Floor = DungeonGenerator.Generate(Config, floorNumber, Random.Shared.Next());
        EnterRoom(Floor.Current, new Position(Config.RoomWidth / 2, Config.RoomHeight / 2));
    }

    // ---- Player actions -----------------------------------------------------

    public void PlayerMove(Direction dir)
    {
        if (!State.Player.IsAlive) return;

        // If standing on a cleared room's door tile, walking into it changes rooms.
        var cur = Floor.Current;
        if (cur.Cleared)
        {
            var dest = State.Player.Position.Move(dir);
            var side = DoorSideAt(cur, dest);
            if (side.HasValue && Floor.Neighbour(side.Value) is { } neighbour)
            {
                var entry = RoomGeometry.EntryTile(Config.RoomWidth, Config.RoomHeight, side.Value);
                EnterRoom(neighbour, entry);
                return; // changing rooms doesn't advance the turn
            }
        }

        State.Player.TryMove(dir, State);
        ProcessMobsTurn();
        State.NextTurn();
        AfterTurn();
    }

    public void PlayerAttack(Direction dir)
    {
        if (!State.Player.IsAlive) return;

        State.Player.Attack(dir, State);
        CleanupDeadMobs();
        ProcessMobsTurn();
        State.NextTurn();
        AfterTurn();
    }

    public void PlayerSkipTurn()
    {
        if (!State.Player.IsAlive) return;

        ProcessMobsTurn();
        State.NextTurn();
        AfterTurn();
    }

    // ---- Turn / room bookkeeping -------------------------------------------

    private void AfterTurn()
    {
        var cur = Floor.Current;
        if (!cur.Cleared && !State.Mobs.Any(m => m.IsAlive))
        {
            cur.Cleared = true;
            OpenDoors(cur);

            // Auto-descend, unless this was the final floor (then the run is won).
            if (Floor.AllCleared && Floor.Number < Config.MaxFloors)
                StartNewRun(Floor.Number + 1);
        }
    }

    private void ProcessMobsTurn()
    {
        foreach (var p in State.Projectiles) p.PerformTurn(State);
        foreach (var m in State.Mobs) m.PerformTurn(State);
        foreach (var e in State.Effects) e.PerformTurn(State);
        CleanupDeadMobs();
    }

    private void CleanupDeadMobs()
    {
        State.Mobs.RemoveAll(m => !m.IsAlive);
        State.Effects.RemoveAll(m => !m.IsAlive);
        State.Projectiles.RemoveAll(m => !m.IsAlive);
    }

    // ---- Room loading -------------------------------------------------------

    private void EnterRoom(DungeonRoom target, Position playerPos)
    {
        Floor.CurrentX = target.GridX;
        Floor.CurrentY = target.GridY;
        target.Visited = true;

        var room = new Room(Config.RoomWidth, Config.RoomHeight);
        ApplyLayout(room, target);

        State.SetRoom(room);
        State.ClearEntities();
        AddWalls(room);

        if (!target.Cleared && !target.Spawned)
            SpawnMobs(target);

        State.Player.Teleport(playerPos);

        // A room with no mobs (e.g. the start room) is already clear.
        if (!target.Cleared && !State.Mobs.Any(m => m.IsAlive))
        {
            target.Cleared = true;
            OpenDoors(target);
        }
    }

    private void ApplyLayout(Room room, DungeonRoom target)
    {
        var w = Config.RoomWidth;
        var h = Config.RoomHeight;

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            room.SetWalkable(new Position(x, y), !border);
        }

        // Cleared rooms have open doorways.
        if (target.Cleared)
            foreach (var dir in target.Doors)
                room.SetWalkable(RoomGeometry.DoorTile(w, h, dir), true);
    }

    private void AddWalls(Room room)
    {
        var w = Config.RoomWidth;
        var h = Config.RoomHeight;

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var pos = new Position(x, y);
            if (!room.IsWalkable(pos))
                State.AddObject(new Wall(pos));
        }
    }

    private void SpawnMobs(DungeonRoom target)
    {
        foreach (var spawn in target.MobSpawns)
            State.AddMob(CreateMob(spawn.Type, spawn.Position));
        target.Spawned = true;
    }

    /// <summary>Opens this room's doorways: makes the tiles walkable and removes their wall entities.</summary>
    private void OpenDoors(DungeonRoom target)
    {
        var room = State.GetCurrentRoom();
        var w = Config.RoomWidth;
        var h = Config.RoomHeight;

        foreach (var dir in target.Doors)
        {
            var door = RoomGeometry.DoorTile(w, h, dir);
            room.SetWalkable(door, true);
            State.StaticObjects.RemoveAll(o => o is Wall && o.Position.Equals(door));
        }
    }

    private Direction? DoorSideAt(DungeonRoom room, Position pos)
    {
        var w = Config.RoomWidth;
        var h = Config.RoomHeight;

        foreach (var dir in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            if (room.Doors.Contains(dir) && RoomGeometry.DoorTile(w, h, dir).Equals(pos))
                return dir;

        return null;
    }

    private static Mob CreateMob(EntityType type, Position pos) => type switch
    {
        EntityType.Lambda => new Lambda(pos),
        EntityType.ModusPonens => new ModusPonens(pos),
        _ => new ModusPonens(pos)
    };
}
