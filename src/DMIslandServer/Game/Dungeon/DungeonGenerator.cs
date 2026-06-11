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

    public static IEnumerable<string> CreateTemplateList(Random rand, int roomCount)
    {
        // 2 Item rooms
        var itemRooms = DungeonRoomTemplateStorage.ChooseN(
            rand, DungeonRoomTemplateStorage.GetItemRooms(), 2
        );
        // 1 Exit room
        var exitRoom = DungeonRoomTemplateStorage.ChooseN(
            rand, DungeonRoomTemplateStorage.GetExitRooms(), 1
        );
        // Anything else is normal
        var others = DungeonRoomTemplateStorage.ChooseN(
            rand, DungeonRoomTemplateStorage.GetNormalRoomSources(), roomCount - 3
        );
        return itemRooms.Concat(others).Concat(exitRoom);
    }
    
    public static Floor Generate(GameConfig config, int floorNumber, int seed)
    {
        var rand = new Random(seed);
        var rooms = new DungeonRoom?[GridSize, GridSize];
        
        var floorBiomes = new[] { "beach", "forest", "swamp", "nerd" };
        var biome = floorBiomes[(floorNumber - 1) % floorBiomes.Length];

        var cx = GridSize / 2;
        var cy = GridSize / 2;

        var start = new DungeonRoom(cx, cy, DungeonRoomTemplate.Empty(floorNumber))
        {
            IsStart = true, Visited = true, Cleared = true
        };
        rooms[cx, cy] = start;

        var placed = new List<DungeonRoom> { start };

        var targetRooms = Math.Min(
            GridSize * GridSize,
            config.BaseRooms + (floorNumber - 1) * config.RoomsPerFloor
        );

        var templates = CreateTemplateList(rand, targetRooms)
            .Select(x => DungeonRoomTemplate.OfString(rand, floorNumber, x))
            .ToList();

        // Grow the floor by repeatedly attaching a new room to a random existing one.
        var safety = 0;
        var templateIndex = 0;
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

                var room = new DungeonRoom(nx, ny, templates[templateIndex++]);
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
        
        return new Floor(floorNumber, rooms, cx, cy);
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
