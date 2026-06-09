namespace DMIslandClient.World

open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Common.SpriteGroup
open LadaEngine.Engine.Renderables.GroupRendering

type RoomType = MonadicBeach

type IRoomRenderer =
    abstract Render: Camera -> unit
    abstract Update: unit -> unit
    abstract AddTile: Pos -> unit
    
type MBRoomRenderer() =
    let textures = [| Resources.Texture.SAND; Resources.Texture.SANDSTONE |]
    let atlas = TextureAtlas(textures)
    let group = SpriteGroup(atlas)

    let addTile (pos: Pos) =
        let texture = GameRandom.choice(textures)
        let sprite = Sprite(pos, atlas, texture)
        sprite.Width <- 1f
        sprite.Height <- 1f
        group.AddSprite(sprite)
        
    interface IRoomRenderer with
        member _.Render(camera) = group.Render(camera)
        member _.AddTile(pos) = addTile pos
        member _.Update() = group.Update()
    

module RoomRenderer =
    let getFor(t: RoomType): IRoomRenderer =
        match t with
        | MonadicBeach -> MBRoomRenderer()