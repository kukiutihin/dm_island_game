using System.Collections.Generic;
using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Dungeon;

/// <summary>A mob to be created when the room is first entered.</summary>
public class MobSpawn(EntityType type, Position position)
{
    public EntityType Type => type;
    public Position Position => position;
}

public class ItemSpawn(ItemType type, Position position)
{
    public ItemType Type => type;
    public Position Position => position;
}

/// <summary>
/// One room of a dungeon floor. Holds its grid coordinates, which sides have
/// doors, the mob pack to spawn, and progress flags.
/// </summary>
public class DungeonRoom(int gridX, int gridY, DungeonRoomTemplate template)
{
    public int GridX => gridX;
    public int GridY => gridY;

    /// <summary>Sides of the room that have a door to a neighbouring room.</summary>
    public HashSet<Direction> Doors { get; } = [];

    public readonly DungeonRoomTemplate Template = template;

    /// <summary>Visual theme of the room: "beach", "forest", "cave", "snow".</summary>
    public string Biome { get; set; } = "beach";

    public bool IsStart { get; set; }
    public bool Visited { get; set; }
    public bool Cleared { get; set; }

    /// <summary>True once this room's mobs have been instantiated into the live world.</summary>
    public bool Spawned { get; set; }
}
