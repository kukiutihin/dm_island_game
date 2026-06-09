namespace DMIslandClient.Connection

open System
open System.Collections.Generic
open System.Text.Json.Serialization

module Dto =
    type EventType =
        | EntityDeath = 0
        | TearPop = 1

    type EntityType =
        | Player = 0
        | ModusPonens = 1
        | Lambda = 2
        | Monad = 3
        | Tear = 4
        | Wall = 5

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
    type PlayerViewDto = {
        [<JsonPropertyName("id")>]
        Id: Guid
        
        [<JsonPropertyName("name")>]
        Name: string
        
        [<JsonPropertyName("hp")>]
        Hp: int
        
        [<JsonPropertyName("maxHp")>]
        MaxHp: int
        
        [<JsonPropertyName("position")>]
        Position: PositionDto
    }

    [<CLIMutable>]
    type ObjectViewDto = {
        [<JsonPropertyName("id")>]
        Id: Guid
        
        [<JsonPropertyName("type")>]
        Type: string
        
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
        
        [<JsonPropertyName("objects")>]
        Objects: List<ObjectViewDto>
        
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
        Player: PlayerViewDto

        [<JsonPropertyName("objects")>]
        Objects: List<ObjectViewDto>

        [<JsonPropertyName("viewWidth")>]
        ViewWidth: int

        [<JsonPropertyName("viewHeight")>]
        ViewHeight: int

        [<JsonPropertyName("events")>]
        Events: List<EventDto>

        [<JsonPropertyName("floor")>]
        Floor: int

        [<JsonPropertyName("rooms")>]
        Rooms: List<RoomCellDto>
    }
