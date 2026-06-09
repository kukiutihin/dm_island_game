namespace DMIslandClient

open System
open DMIslandClient.Connection.Dto
open DMIslandClient.Effect
open DMIslandClient.Entity
open DMIslandClient.UI
open LadaEngine.Engine.Base

type EventDispatcher(entities: EntityGroup, effects: EffectGroup, ui: GameUI) =
    let entityTypeOfString = function
        | "Wall" -> EtWall
        | "Lambda" -> EtLambda
        | "ModusPonens" -> EtModusPonens
        | "Tear" -> EtTear
        | m -> failwith $"Unknown mob type: {m}"
        
    let posOfDto (p: PositionDto) =
        Pos(p.X, p.Y)

    let toQuery (entity: ObjectViewDto) =
        let typ = entityTypeOfString entity.Type
        let pos = posOfDto entity.Position
        let prevPos = posOfDto entity.PreviousPosition
        entity.Id, typ, prevPos, pos    
    
    let processEvent (e: EventDto) =
        match e.Type with
        | EventType.EntityDeath -> effects.CreateEffect(EtEntityDeath, posOfDto e.Position)
        | EventType.TearPop -> effects.CreateEffect(EtTearPop, posOfDto e.Position)
        | _ -> ArgumentOutOfRangeException() |> raise

    let processEntities (objects: ObjectViewDto seq) =
        let query = Seq.map toQuery objects
        entities.CreateOrUpdateAll(query)
    
    let processPlayer (player: PlayerViewDto) =
        let pos = posOfDto player.Position
        ui.SetHealth(player.Hp)
        entities.CreateOrUpdatePlayer(player.Id, pos)
    
    member public x.ProcessUpdate(data: GameStateResponse) =
        data.Events |> Seq.iter processEvent
        data.Objects |> processEntities
        data.Player |> processPlayer
        ui.SetFloor(data.Floor)
        ui.SetMinimap(data.Rooms)

