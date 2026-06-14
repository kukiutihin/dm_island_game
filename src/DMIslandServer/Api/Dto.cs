using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using RoguelikeServerMVP.Game;

namespace RoguelikeServerMVP.Api;

public class PlayerActionRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }
}

public class PositionDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    public PositionDto(int x, int y)
    {
        X = x;
        Y = y;
    }

    public PositionDto(Position position)
    {
        X = position.X;
        Y = position.Y;
    }
}

public class RoomDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("biome")]
    public string Biome { get; set; } = "beach";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class ObjectViewDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EntityType Type { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("position")]
    public PositionDto Position { get; set; }

    [JsonPropertyName("previous_position")]
    public PositionDto PreviousPosition { get; set; }
}

public class GameStateResponse
{
    [JsonPropertyName("turn")]
    public int Turn { get; set; }

    [JsonPropertyName("player")]
    public ObjectViewDto Player { get; set; } = new();

    [JsonPropertyName("entities")]
    public List<ObjectViewDto> Entities { get; set; } = [];
    
    [JsonPropertyName("objects")]
    public List<ObjectViewDto> Objects { get; set; } = [];
    
    [JsonPropertyName("events")]
    public List<EventDto> Events { get; set; } = [];

    [JsonPropertyName("viewWidth")]
    public int ViewWidth { get; set; }

    [JsonPropertyName("viewHeight")]
    public int ViewHeight { get; set; }

    [JsonPropertyName("floor")]
    public int Floor { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
    
    [JsonPropertyName("items")]
    public List<ItemType>? Items { get; set; }

    [JsonPropertyName("room")]
    public RoomDto Room { get; set; }

    [JsonPropertyName("rooms")]
    public List<RoomCellDto> Rooms { get; set; } = [];
}

public class RoomCellDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("visited")]
    public bool Visited { get; set; }

    [JsonPropertyName("cleared")]
    public bool Cleared { get; set; }

    [JsonPropertyName("current")]
    public bool Current { get; set; }

    /// <summary>True when this room holds the (active) floor exit — used to mark it on the minimap.</summary>
    [JsonPropertyName("isExit")]
    public bool IsExit { get; set; }
}

public class EventDto
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventType Type { get; set; }

    [JsonPropertyName("position")]
    public Position Position { get; set; } = Position.Zero;
    
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}


public enum EventType
{
    PlayerDied,
    EntityDeath,
    EnemyProjectilePop,
    TearPop
}

public enum EntityType
{
    Player, 
    
    ModusPonens,
    Lambda,
    Monad,
    Nerd,
    NuclearNerd,
    Skolem,
    Mole,
    Tear,
    
    AttackIndicator,
    ThetaAttack,
    Lightning,
    EnemyProjectile,
    Wall,
    Exit,
    ExitClosed,

    HeartItem,
    HalfHeartItem,
    AmethystItem,

    CppItem,
    HaskellItem,
    Python3Item,
    JavaItem,
    OCamlItem,
    ZigItem,
    RustItem,
    AnsiCItem,
    FSharpItem,
    RocItem,
    OneFItem,
    JavaScriptItem,
    TypeScriptItem,
    GoItem,
    KotlinItem,
    AsmItem,
    Scala3Item
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemType
{
    Heart,
    HalfHeart,
    Amethyst,
    
    Cpp, 
    Haskell,
    Python3,
    Java,
    OCaml,
    Zig,
    Rust,
    AnsiC,
    FSharp,
    Roc,
    OneF,
    JavaScript,
    TypeScript,
    Go,
    Kotlin,
    Asm,
    Scala3
}