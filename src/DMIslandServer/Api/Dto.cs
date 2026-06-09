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

public class PlayerViewDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("position")]
    public PositionDto Position { get; set; } = new(Game.Position.Zero);
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
    public PlayerViewDto Player { get; set; } = new();

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
    TearPop
}

public enum EntityType
{
    Player, 
    ModusPonens,
    Lambda,
    Monad,
    Tear,
    Wall,

    CppItem,
    HaskellItem,
    Python3Item,
}

public enum ItemType
{
    Cpp, 
    Haskell,
    Python3,
}