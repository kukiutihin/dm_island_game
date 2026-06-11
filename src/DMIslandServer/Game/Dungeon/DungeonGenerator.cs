using System;
using System.Collections.Generic;
using System.Linq;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Pickups;

namespace RoguelikeServerMVP.Game.Dungeon;

/// <summary>
/// Generates an Isaac-style floor: an irregular, connected cluster of rooms on
/// a grid. The centre room is the (cleared) start; every other room holds a
/// pack of mobs. Deeper floors get more rooms and bigger packs.
/// </summary>
public static class DungeonGenerator
{
    private const int GridSize = 9;

    private static readonly Direction[] AllDirections =
        [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    public static Floor Generate(GameConfig config, int floorNumber, int seed)
    {
        var rand = new Random(seed);
        var rooms = new DungeonRoom?[GridSize, GridSize];
        
        var floorBiomes = new[] { "beach", "forest", "swamp", "nerd" };
        var biome = floorBiomes[(floorNumber - 1) % floorBiomes.Length];

        var cx = GridSize / 2;
        var cy = GridSize / 2;

        var start = new DungeonRoom(cx, cy) { IsStart = true, Visited = true, Cleared = true };
        rooms[cx, cy] = start;

        var placed = new List<DungeonRoom> { start };

        var targetRooms = Math.Min(
            GridSize * GridSize,
            config.BaseRooms + (floorNumber - 1) * config.RoomsPerFloor);

        // Grow the floor by repeatedly attaching a new room to a random existing one.
        var safety = 0;
        while (placed.Count < targetRooms && safety++ < 10_000)
        {
            var anchor = placed[rand.Next(placed.Count)];

            foreach (var dir in Shuffle(AllDirections, rand))
            {
                var (dx, dy) = RoomGeometry.Delta(dir);
                var nx = anchor.GridX + dx;
                var ny = anchor.GridY + dy;

                if (nx < 0 || ny < 0 || nx >= GridSize || ny >= GridSize) continue;
                if (rooms[nx, ny] != null) continue;

                var room = new DungeonRoom(nx, ny);
                rooms[nx, ny] = room;
                placed.Add(room);
                break;
            }
        }

        // Connect every pair of orthogonally-adjacent placed rooms with doors.
        foreach (var room in placed)
        {
            foreach (var dir in AllDirections)
            {
                var (dx, dy) = RoomGeometry.Delta(dir);
                var nx = room.GridX + dx;
                var ny = room.GridY + dy;
                if (nx < 0 || ny < 0 || nx >= GridSize || ny >= GridSize) continue;
                if (rooms[nx, ny] == null) continue;
                room.Doors.Add(dir);
            }
        }
        
        foreach (var room in placed)
            room.Biome = biome;

        // Populate mob packs (every room except the start).
        var packSize = config.MobPackSize + (floorNumber - 1);
        foreach (var room in placed.Where(r => !r.IsStart))
            PopulatePack(room, config, packSize, rand);
        
        for (var i = 0; i < 2; i++) 
        {
            var itemRoom = placed[rand.Next(placed.Count)];
            CreateItem(itemRoom, rand);
        }

        return new Floor(floorNumber, rooms, cx, cy);
    }

    private static void CreateItem(DungeonRoom room, Random rand)
    {
        List<ItemType> variants = [ItemType.Haskell,
            ItemType.Python3, ItemType.Cpp, ItemType.Java,
            ItemType.OCaml, ItemType.Zig, ItemType.Rust,
            ItemType.AnsiC, ItemType.FSharp, ItemType.Roc,
            ItemType.OneF, ItemType.JavaScript, ItemType.TypeScript,
            ItemType.Go, ItemType.Kotlin, ItemType.Asm, ItemType.Scala3
        ];
        var type = variants[rand.Next(variants.Count)];
        var position = new Position(7, 5);
        room.ItemSpawns.Add(new ItemSpawn(type, position));
    }

    private static void PopulatePack(DungeonRoom room, GameConfig config, int packSize, Random rand)
    {
        var count = packSize + rand.Next(0, 1);
        var used = new HashSet<(int, int)>();

        for (var i = 0; i < count; i++)
        {
            // Pick a free interior tile (avoid the border).
            int x, y, tries = 0;
            do
            {
                x = 1 + rand.Next(config.RoomWidth - 2);
                y = 1 + rand.Next(config.RoomHeight - 2);
                tries++;
            }
            while (used.Contains((x, y)) && tries < 20);

            used.Add((x, y));

            List<EntityType> enemyTypes = [
                EntityType.ModusPonens,
                EntityType.Lambda,
                EntityType.Monad,
                EntityType.Nerd,
                EntityType.NuclearNerd,
                EntityType.Skolem,
                EntityType.Mole,
                EntityType.Tear,
            ];
            var enemyType = enemyTypes[rand.Next(enemyTypes.Count)];
            room.MobSpawns.Add(new MobSpawn(enemyType, new Position(x, y)));
        }
    }

    private static List<Direction> Shuffle(Direction[] source, Random rand)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
