namespace DMIslandClient

open System
open DMIslandClient.Connection.Dto
open DMIslandClient.Effect
open DMIslandClient.Entity
open DMIslandClient.Resources
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
        | EventType.EnemyProjectilePop ->
            entities.MoveEntityTo(Guid.Parse(e.Payload), posOfDto e.Position)
            effects.CreateEffect(EtProjectilePop, posOfDto e.Position)
        | EventType.MobAttack ->
            // Melee hit: spark burst at the mob plus a quick camera kick.
            effects.CreateEffect(EtMobAttack, posOfDto e.Position)
            camera.Shake(0.35f)
        | EventType.NeironkaVisual ->
            let parts = e.Payload.Split('|')
            if parts.Length = 2 then
                let texture =
                    match parts.[1] with
                    | "attack" -> Resources.Entity.NEIRONKA_ATTACK
                    | "hide" -> Resources.Entity.NEIRONKA_HIDE
                    | _ -> Resources.Entity.NEIRONKA_IDLE
                entities.PlayAnimation(Guid.Parse(parts.[0]), [| texture |], 0f, false)
                if parts.[1] = "hide" then effects.CreateEffect(EtBlueFlash, posOfDto e.Position)
        | EventType.NeironkaBoom ->
            effects.CreateEffect(EtBlueFlash, posOfDto e.Position)
        | _ -> ArgumentOutOfRangeException() |> raise

    let processEntities (updates: ObjectViewDto seq)=
        let query = Seq.map toQuery updates
        entities.CreateOrUpdateAll(query)
    
    let processObjects (updates: ObjectViewDto seq) =
        let query = Seq.map toQuery updates
        objects.CreateOrUpdateAll(query)
    
    let processPlayer (player: ObjectViewDto) =
        ui.SetHealth(player.Hp)
        ui.SetMaxHealth(player.MaxHp)
        camera.SetPosition(posOfDto player.Position)
    
    let processItems (items: ItemType seq) =
        ui.SetItems(items)
    
    member public x.ProcessUpdate(data: GameStateResponse) =
        data.Events |> Seq.iter processEvent
        data.Entities |> Seq.append [ data.Player ] |> processEntities
        data.Objects |> processObjects
        data.Player |> processPlayer
        data.Items |> processItems
        ui.SetFloor(data.Floor)
        ui.SetMinimap(data.Rooms)

