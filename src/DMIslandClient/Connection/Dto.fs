namespace DMIslandClient.Connection

open System
open System.Collections.Generic
open System.Text.Json.Serialization

module Dto =
    type EventType =
        | EntityDeath = 1
        | TearPop = 2

    type EntityType =
        | Player = 1
        | ModusPonens = 2
        | Lambda = 3
        | Monad = 4
        | Tear = 5
        | EnemyProjectile = 6
        | Wall = 7
        | HeartItem = 8
        | HalfHeartItem = 9
        | AmethystItem = 10
        | CppItem = 11
        | HaskellItem = 12
        | Python3Item = 13

    [<CLIMutable>]
    type PlayerActionRequest = {
        [<JsonPropertyName("action")>]
        Action: string
        
        [<JsonPropertyName("direction")>]
        Direction: string option
    }

    [<CLIMutable>]
    type PositionDto = {
        [<JsonPropertyName("x")>]
        X: int
        
        [<JsonPropertyName("y")>]
        Y: int
    }
    
    [<CLIMutable>]
    type RoomDto = {
        [<JsonPropertyName("id")>]
        Id: Guid
        
        [<JsonPropertyName("biome")>]
        Biome: String
    }

    [<CLIMutable>]
    type ObjectViewDto = {
        [<JsonPropertyName("id")>]
        Id: Guid
        
        [<JsonPropertyName("type")>]
        [<JsonConverter(typeof<JsonStringEnumConverter>)>]
        Type: EntityType
        
        [<JsonPropertyName("name")>]
        Name: string
        
        [<JsonPropertyName("hp")>]
        Hp: int
        
        [<JsonPropertyName("maxHp")>]
        MaxHp: int
        
        [<JsonPropertyName("position")>]
        Position: PositionDto
        
        [<JsonPropertyName("previous_position")>]
        PreviousPosition: PositionDto
    }
    
    [<CLIMutable>]
    type EventDto = {
        [<JsonPropertyName("type")>]
        [<JsonConverter(typeof<JsonStringEnumConverter>)>]
        Type: EventType
        
        [<JsonPropertyName("position")>]
        Position: PositionDto
        
        [<JsonPropertyName("payload")>]
        Payload: string
    }

    [<CLIMutable>]
    type RoomCellDto = {
        [<JsonPropertyName("x")>]
        X: int

        [<JsonPropertyName("y")>]
        Y: int

        [<JsonPropertyName("visited")>]
        Visited: bool

        [<JsonPropertyName("cleared")>]
        Cleared: bool

        [<JsonPropertyName("current")>]
        Current: bool
    }

    [<CLIMutable>]
    type GameStateResponse = {
        [<JsonPropertyName("turn")>]
        Turn: int

        [<JsonPropertyName("player")>]
        Player: ObjectViewDto

        [<JsonPropertyName("objects")>]
        Objects: List<ObjectViewDto>
        
        [<JsonPropertyName("entities")>]
        Entities: List<ObjectViewDto>

        [<JsonPropertyName("viewWidth")>]
        ViewWidth: int

        [<JsonPropertyName("viewHeight")>]
        ViewHeight: int

        [<JsonPropertyName("events")>]
        Events: List<EventDto>

        [<JsonPropertyName("floor")>]
        Floor: int

        [<JsonPropertyName("completed")>]
        Completed: bool

        [<JsonPropertyName("room")>]
        Room: RoomDto

        [<JsonPropertyName("rooms")>]
        Rooms: List<RoomCellDto>
    }
