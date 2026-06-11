namespace DMIslandClient.Connection

open System
open System.Collections.Generic
open System.Text.Json.Serialization

module Dto =
    type EventType =
        | EntityDeath = 1
        | TearPop = 2
        | EnemyProjectilePop = 3

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
        | JavaItem = 14
        | OCamlItem = 15
        | ZigItem = 16
        | RustItem = 17
        | AnsiCItem = 18
        | FSharpItem = 19
        | RocItem = 20
        | OneFItem = 21
        | JavaScriptItem = 22
        | TypeScriptItem = 23
        | GoItem = 24
        | KotlinItem = 25
        | AsmItem = 26
        | Scala3Item = 27
        | Nerd = 28
        | NuclearNerd = 29
        | Skolem = 30
        | Mole = 31
        | AttackIndicator = 32
        | ThetaAttack = 33

    type ItemType =
        | Heart = 1
        | HalfHeart = 2
        | Amethyst = 3
        
        | Cpp = 4
        | Haskell = 5
        | Python3 = 6
        | Java = 7
        | OCaml = 8
        | Zig = 9
        | Rust = 10
        | AnsiC = 11
        | FSharp = 12
        | Roc = 13
        | OneF = 14
        | JavaScript = 15
        | TypeScript = 16
        | Go = 17
        | Kotlin = 18
        | Asm = 19
        | Scala3 = 20
    
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
        
        [<JsonPropertyName("items")>]
        Items: List<ItemType>

        [<JsonPropertyName("completed")>]
        Completed: bool

        [<JsonPropertyName("room")>]
        Room: RoomDto

        [<JsonPropertyName("rooms")>]
        Rooms: List<RoomCellDto>
    }
