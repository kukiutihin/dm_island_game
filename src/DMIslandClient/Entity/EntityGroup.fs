namespace DMIslandClient.Entity

open System
open System.Collections.Generic
open DMIslandClient.Connection.Dto
open LadaEngine

type EntityUpdateQuery = {
    id: Guid
    previousPosition: Pos
    position: Pos
    typ: EntityType
}

type EntityGroup(textures, entityFactory: IEntityFactory) =
    let atlas = TextureAtlas(textures)
    let spriteGroup = SpriteGroup(atlas)
    let entities = Dictionary<Guid, Entity>()

    member x.CreateOrUpdate(id: Guid, t: EntityType, prevPos: Pos, pos: Pos)=
        match entities.TryGetValue(id) with
        | false, _ ->
            let entity = entityFactory.CreateEntity(t, atlas, spriteGroup, prevPos)
            entity.SetTarget(pos)
            entities.Add(id, entity)
        | true, v ->
            if Pos.len (v.GetTarget()) pos > 5f then
                v.Teleport(pos)
            v.SetTarget(pos)
    
    member x.RemoveEntity(id: Guid) =
        let entity = entities[id]
        spriteGroup.Sprites.Remove(entity.Sprite) |> ignore
        entities.Remove(id) |> ignore
    
    member x.CreateOrUpdateAll(query : EntityUpdateQuery seq) =
        let updatedIds = Seq.map _.id query |> HashSet
        Seq.iter (fun q -> x.CreateOrUpdate(q.id, q.typ, q.previousPosition, q.position))  query
        entities.Keys
        |> Seq.filter (fun id -> not (updatedIds.Contains(id)))
        |> Seq.iter x.RemoveEntity

    member x.Render(camera: Camera) =
        spriteGroup.Render(camera)
        
    member x.Update(dt) =
        Seq.iter (fun (e: Entity) -> e.Update(dt)) entities.Values
        spriteGroup.Update()

    member x.PlayAnimation(id: Guid, frames: string[], fps: float32, looping: bool) =
        match entities.TryGetValue(id) with
        | true, e -> e.SetAnimation(frames, fps, looping = looping)
        | false, _ -> ()

    member x.MoveEntityTo(guid: Guid, pos: Pos) =
        match entities.TryGetValue(guid) with
        | true, ent ->
            if Pos.len (ent.GetTarget()) pos > 5f then
                ent.Teleport(pos)
            ent.SetTarget(pos)
        | false, _ -> ()

    member x.GetGroup() = spriteGroup