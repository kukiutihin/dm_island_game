using System;
using System.Linq;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Dungeon;
using RoguelikeServerMVP.Game.Entities;
using RoguelikeServerMVP.Game.Entities.Factory.Preset;
using RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;
using RoguelikeServerMVP.Game.Entities.Pickups;

namespace RoguelikeServerMVP.Game;

public class GameEngine
{
    public GameState State { get; }
    public GameConfig Config { get; }
    public Floor Floor { get; private set; }

    /// <summary>The dungeon room currently loaded into the live state, so we can save its items on leave.</summary>
    private DungeonRoom? _loadedRoom;

    /// <summary>Source of per-floor generation seeds. Re-seed via StartWithSeed for reproducible runs.</summary>
    private Random _seedSource = new Random();

    public GameEngine(GameConfig config)
    {
        Config = config;

        var player = new Player(
            new Position(config.RoomWidth / 2, config.RoomHeight / 2),
            config.PlayerDefaultMaxHp);

        State = new GameState(player, new Room(config.RoomWidth, config.RoomHeight));

        StartNewRun(1);
    }

    /// <summary>Begins a brand-new run from floor 1 with a healed player (used on restart).</summary>
    public void Restart()
    {
        State.Player.RestoreFullHealth();
        StartNewRun(1);
    }

    /// <summary>
    /// Begins a fresh, reproducible run from floor 1 using <paramref name="seed"/> as the
    /// generation seed (used by the eval harness's /start_game). Same seed → same floors.
    /// </summary>
    public void StartWithSeed(int seed)
    {
        _seedSource = new Random(seed);
        State.Player.RestoreFullHealth();
        StartNewRun(1);
    }

    private void StartNewRun(int floorNumber)
    {
        Floor = DungeonGenerator.Generate(Config, floorNumber, _seedSource.Next());
        var start = Floor.Current;
        EnterRoom(start, new Position(start.Width / 2, start.Height / 2));
    }

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
                // Land just inside the destination room, sized to that room's dimensions.
                var entry = RoomGeometry.EntryTile(neighbour.Width, neighbour.Height, side.Value);
                EnterRoom(neighbour, entry);
                return; // changing rooms doesn't advance the turn
            }
        }

        State.Player.TryMove(dir, State);

        // Walking onto the open exit portal carries the player to the next floor.
        if (TryUseExit()) return; // changing floors doesn't advance the turn

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

    private void AfterTurn()
    {
        var cur = Floor.Current;
        // A room clears (doors open) once its mobs are gone; leftover items don't keep it locked.
        var mobsLeft = State.Mobs.Any(m => m.IsAlive);
        if (!cur.Cleared && !mobsLeft)
        {
            cur.Cleared = true;
            OpenDoors(cur);

            // Once the whole floor is cleared, the exit "opens". If the player happens
            // to be in the exit room when that happens, activate the portal now;
            // otherwise it's activated when they (re-)enter the exit room.
            if (Floor.AllCleared)
                SyncExitPortal(cur);
        }
    }

    private void ProcessMobsTurn()
    {
        foreach (var p in State.Projectiles) p.PerformTurn(State);
        foreach (var p in State.DelayedProjectiles) p.PerformTurn(State);
        foreach (var m in State.Mobs) m.PerformTurn(State);
        foreach (var e in State.Items) e.PerformTurn(State);
        State.Projectiles.AddRange(State.DelayedProjectiles);
        State.DelayedProjectiles.Clear();
        CleanupDeadMobs();
    }

    private void CleanupDeadMobs()
    {
        State.Mobs.RemoveAll(m => !m.IsAlive);
        State.Items.RemoveAll(m => !m.IsAlive);
        State.Projectiles.RemoveAll(m => !m.IsAlive);
    }

    private void EnterRoom(DungeonRoom target, Position playerPos)
    {
        // Persist the room we're leaving so its dropped/leftover items aren't forgotten.
        SaveCurrentRoomState();

        Floor.CurrentX = target.GridX;
        Floor.CurrentY = target.GridY;
        target.Visited = true;

        var room = new Room(target.Width, target.Height);
        ApplyLayout(room, target);

        State.SetRoom(room);
        State.ClearEntities();
        AddWalls(room);
        AddTemplateWalls(target);

        if (!target.Spawned)
            SpawnEntities(target);          // first visit: spawn mobs + items from the template
        else
            RestoreRoomState(target);       // later visits: restore this room's own saved items

        State.Player.Teleport(playerPos);
        _loadedRoom = target;

        // Door opening depends on mobs only — leftover items never trap the player.
        var mobsLeft = State.Mobs.Any(m => m.IsAlive);

        // A room with no mobs (e.g. the start room) is already clear.
        if (!target.Cleared && !mobsLeft)
        {
            target.Cleared = true;
            OpenDoors(target);
        }

        // The exit room always shows a portal: deactivated until the floor is cleared, then active.
        SyncExitPortal(target);
    }

    /// <summary>
    /// Ensures the exit room holds a portal matching the floor's state: deactivated
    /// (<see cref="EntityType.ExitClosed"/>) while mobs remain anywhere on the floor,
    /// active (<see cref="EntityType.Exit"/>) once the whole floor is cleared.
    /// </summary>
    private void SyncExitPortal(DungeonRoom target)
    {
        if (!target.IsExit || target.ExitTile is not { } tile) return;

        var shouldBeActive = Floor.AllCleared;
        var existing = State.StaticObjects.OfType<Exit>().FirstOrDefault();
        if (existing is not null && existing.IsActive == shouldBeActive) return; // already correct

        if (existing is not null) State.StaticObjects.Remove(existing);
        State.AddObject(new Exit(tile, shouldBeActive));
    }

    /// <summary>If the player is standing on an *active* exit portal, advance to the next floor. Returns true if so.</summary>
    private bool TryUseExit()
    {
        var onActivePortal = State.StaticObjects
            .OfType<Exit>()
            .Any(e => e.IsActive && e.Position.Equals(State.Player.Position));
        if (!onActivePortal) return false;

        // Last floor is won via the Completed flag (floor fully cleared); only descend below it.
        if (Floor.Number < Config.MaxFloors)
            StartNewRun(Floor.Number + 1);

        return true;
    }

    /// <summary>Saves the live items of the currently-loaded room back onto it, so they persist while away.</summary>
    private void SaveCurrentRoomState()
    {
        if (_loadedRoom is null) return;
        _loadedRoom.SavedItems.Clear();
        _loadedRoom.SavedItems.AddRange(State.Items.Where(i => i.IsAlive));
    }

    /// <summary>Restores a previously-visited room's own saved items into the live state.</summary>
    private void RestoreRoomState(DungeonRoom target)
    {
        foreach (var item in target.SavedItems)
            State.AddItem(item);
    }

    private void ApplyLayout(Room room, DungeonRoom target)
    {
        var w = room.Width;
        var h = room.Height;

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
        var w = room.Width;
        var h = room.Height;

        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var pos = new Position(x, y);
            if (!room.IsWalkable(pos))
                State.AddObject(new Wall(pos));
        }
    }

    private void SpawnEntities(DungeonRoom target)
    {
        foreach (var spawn in target.Template.MobSpawns)
            State.AddMob(CreateMob(spawn.Type, spawn.Position));
        foreach (var spawn in target.Template.ItemSpawns)
            State.AddItem(CreateItem(spawn.Type, spawn.Position));
        target.Spawned = true;
    }

    /// <summary>Re-adds the template's interior walls (static layout) on every visit.</summary>
    private void AddTemplateWalls(DungeonRoom target)
    {
        foreach (var pos in target.Template.WallSpawns)
            State.AddObject(new Wall(pos));
    }

    /// <summary>Opens this room's doorways: makes the tiles walkable and removes their wall entities.</summary>
    private void OpenDoors(DungeonRoom target)
    {
        var room = State.GetCurrentRoom();
        var w = room.Width;
        var h = room.Height;

        foreach (var dir in target.Doors)
        {
            var door = RoomGeometry.DoorTile(w, h, dir);
            room.SetWalkable(door, true);
            State.StaticObjects.RemoveAll(o => o is Wall && o.Position.Equals(door));
        }
    }

    private Direction? DoorSideAt(DungeonRoom room, Position pos)
    {
        var w = room.Width;
        var h = room.Height;

        foreach (var dir in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            if (room.Doors.Contains(dir) && RoomGeometry.DoorTile(w, h, dir).Equals(pos))
                return dir;

        return null;
    }

    private static Mob CreateMob(EntityType type, Position pos) => type switch
    {
        EntityType.Lambda => new Lambda(pos),
        EntityType.ModusPonens => new ModusPonens(pos),
        EntityType.Nerd => new Nerd(pos),
        EntityType.NuclearNerd => new NuclearNerd(pos),
        EntityType.Skolem => new Skolem(pos),
        EntityType.Monad => new Monad(pos),
        EntityType.Mole => new Mole(pos),
        EntityType.GoldenFreddy => new GoldenFreddy(pos),
        EntityType.Neironka => new Neironka(pos),
        _ => new ModusPonens(pos)
    };

    private static Item CreateItem(ItemType type, Position pos) => new Item(type, pos);
}
