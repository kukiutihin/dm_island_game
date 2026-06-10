namespace DMIslandClient.World

open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Connection.Dto
open DMIslandClient.Entity
open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine

module WallTextures =
    let textures = [|
        Resources.Texture.STONE_1
        Resources.Texture.STONE_2
    |]

type StaticObjectFactory(wallTextures: string seq) =
    let createWall atlas (group: SpriteGroup) pos =
        let texture = GameRandom.choice wallTextures
        let sprite = Sprite(pos, atlas, texture)
        group.AddSprite(sprite)
        Entity(sprite, LinearAnimatablePos(1f, pos))
    
    interface IEntityFactory with
        member _.CreateEntity(t, atlas, group, pos) =
            match t with
            | EntityType.Wall -> createWall atlas group pos
    

type StaticObjectGroup() =
    let factory = StaticObjectFactory(WallTextures.textures)
    let actualGroup = EntityGroup(WallTextures.textures, factory)
    member x.GetGroup() = actualGroup
