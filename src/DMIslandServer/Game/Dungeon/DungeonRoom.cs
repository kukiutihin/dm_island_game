using System.Collections.Generic;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Pickups;

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

    /// <summary>Tile dimensions of this room. Most rooms use the config default; the exit room is larger.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

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

    /// <summary>True if this is the floor's exit room (holds the portal to the next floor).</summary>
    public bool IsExit { get; set; }

    /// <summary>The tile where the exit portal sits (only meaningful when <see cref="IsExit"/>).</summary>
    public Position? ExitTile { get; set; }

    /// <summary>
    /// Items left on this room's floor (un-collected pickups, mob drops). Persisted
    /// when the player leaves so the room keeps its own state, and restored on return.
    /// </summary>
    public List<Item> SavedItems { get; } = [];
}
