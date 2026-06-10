namespace DMIslandClient

open System
open DMIslandClient.Connection.Dto
open DMIslandClient.Effect
open DMIslandClient.Entity
open DMIslandClient.UI
open LadaEngine

type EventDispatcher(entities: EntityGroup, effects: EffectGroup, objects: EntityGroup, ui: GameUI, camera: ElasticCamera) =
    let posOfDto (p: PositionDto) =
        Pos(p.X, p.Y)

    let toQuery (entity: ObjectViewDto) : EntityUpdateQuery=
        let typ = entity.Type
        let pos = posOfDto entity.Position
        let prevPos = posOfDto entity.PreviousPosition
        { id = entity.Id; typ = typ; previousPosition = prevPos; position = pos }
    
    let collectEntityId (e: EventDto) =
        match e.Type with
        | EventType.EntityDeath -> Guid.Parse(e.Payload) |> Some
        | EventType.TearPop -> Guid.Parse(e.Payload) |> Some 
        | _ -> None
    
    let processEvent (e: EventDto) =
        match e.Type with
        | EventType.EntityDeath ->
            entities.MoveEntityTo(Guid.Parse(e.Payload), posOfDto e.Position)
            effects.CreateEffect(EtEntityDeath, posOfDto e.Position)
        | EventType.TearPop ->
            entities.MoveEntityTo(Guid.Parse(e.Payload), posOfDto e.Position)
            effects.CreateEffect(EtTearPop, posOfDto e.Position)
        | _ -> ArgumentOutOfRangeException() |> raise

    let processEntities (updates: ObjectViewDto seq)=
        let query = Seq.map toQuery updates
        entities.CreateOrUpdateAll(query)
    
    let processObjects (updates: ObjectViewDto seq) =
        let query = Seq.map toQuery updates
        objects.CreateOrUpdateAll(query)
    
    let processPlayer (player: ObjectViewDto) =
        ui.SetHealth(player.Hp)
        camera.SetPosition(posOfDto player.Position)
    
    member public x.ProcessUpdate(data: GameStateResponse) =
        data.Events |> Seq.iter processEvent
        data.Entities |> Seq.append [ data.Player ] |> processEntities
        data.Objects |> processObjects
        data.Player |> processPlayer
        ui.SetFloor(data.Floor)
        ui.SetMinimap(data.Rooms)

