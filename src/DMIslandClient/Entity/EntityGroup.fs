namespace DMIslandClient.Entity

open System
open System.Collections.Generic
open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Connection.Dto
open DMIslandClient.Resources
open LadaEngine

type EntityUpdateQuery = {
    id: Guid
    previousPosition: Pos
    position: Pos
    typ: EntityType
}

type EntityGroup() =
    let textures = [|
        Resources.Entity.STEVE
        Resources.Entity.LAMBDA
        Resources.Entity.MODUS_PONENS
        Resources.Texture.DIRT
        Resources.Particle.BUBBLE
        Resources.Particle.ENEMY_PROJECTILE
        Resources.Item.CPP
        Resources.Item.HASKELL
        Resources.Item.PYTHON3
    |]
    let atlas = TextureAtlas(textures)
    let spriteGroup = SpriteGroup(atlas)
    let entities = Dictionary<Guid, Entity>()
    let mutable player : Entity option = None
    let mutable playerId : Guid option = None
    
    let createLambda pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.LAMBDA)
        let entity = Entity(sprite, EaseOutAnimatablePos(4f, pos))
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity
    
    let createMp pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.MODUS_PONENS)
        let entity = Entity(sprite, SmoothAnimatablePos(4f, pos))
        sprite.Height <- 0.4f
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity

    let createWall pos =
        let sprite = Sprite(pos, atlas, Resources.Texture.DIRT)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, SmoothAnimatablePos(1f, pos))
        
    let createProjectile texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, SmoothAnimatablePos(4f, pos))

    let createPlayer pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.STEVE)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos))
        entity.SetFlip(true)
        entity
        
    let createItem texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        sprite.Height <- 0.6f
        sprite.Width <- 0.6f
        Entity(sprite, SmoothAnimatablePos(4f, pos))
    
    let createNewEntity id t prevPos pos=
        let createEntity =
            match t with
            | EntityType.Lambda -> createLambda
            | EntityType.ModusPonens -> createMp
            | EntityType.Wall -> createWall
            | EntityType.Tear -> createProjectile Resources.Particle.BUBBLE
            | EntityType.EnemyProjectile -> createProjectile Resources.Particle.ENEMY_PROJECTILE
            | EntityType.CppItem -> createItem Resources.Item.CPP
            | EntityType.Python3Item -> createItem Resources.Item.PYTHON3
            | EntityType.HaskellItem -> createItem Resources.Item.HASKELL
            | _ -> failwith "Out of range for entity type"
        let entity = createEntity prevPos
        entity.SetTarget pos
        entities.Add(id, entity)

    member x.CreateOrUpdate(id: Guid, t: EntityType, prevPos: Pos, pos: Pos)=
        match entities.TryGetValue(id) with
        | true, v -> v.SetTarget(pos)
        | false, _ -> createNewEntity id t prevPos pos
    
    member x.RemoveEntity(id: Guid) =
        let entity = entities[id]
        spriteGroup.Sprites.Remove(entity.Sprite) |> ignore
        entities.Remove(id) |> ignore
    
    member x.CreateOrUpdateAll(query : EntityUpdateQuery seq) =
        let isPlayerId id = playerId = Some id
        let updatedIds = Seq.map _.id query |> HashSet
        Seq.iter (fun q -> x.CreateOrUpdate(q.id, q.typ, q.previousPosition, q.position))  query
        entities.Keys
        |> Seq.filter (fun id -> not (updatedIds.Contains(id) || isPlayerId id))
        |> Seq.iter x.RemoveEntity

    member x.CreateOrUpdatePlayer(id: Guid, pos: Pos) =
        match player with
        | None ->
            let playerEntity = createPlayer pos
            entities.Add(id, playerEntity)
            player <- Some playerEntity
            playerId <- Some id
        | Some x -> x.SetTarget(pos)
    
    member x.Render(camera: Camera) =
        spriteGroup.Render(camera)
        
    member x.Update(dt) =
        Seq.iter (fun (e: Entity) -> e.Update(dt)) entities.Values
        spriteGroup.Update()
        
    member x.GetPlayer() : Entity option = player
    
    member x.MoveEntityTo(guid: Guid, pos: Pos) =
        match entities.TryGetValue(guid) with
        | true, ent -> ent.Position.SetTarget(pos)
        | false, _ -> ()
