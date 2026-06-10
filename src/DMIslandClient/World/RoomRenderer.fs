namespace DMIslandClient.World

open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine

type RoomType =
    | Beach
    | Forest
    | Cave
    | Snow

type IRoomRenderer =
    abstract Render: Camera -> unit
    abstract Update: unit -> unit
    abstract AddTile: Pos -> unit

/// A floor renderer that randomly tiles the room with the given textures.
type TiledRoomRenderer(textures: string[]) =
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
    let getFor (t: RoomType) : IRoomRenderer =
        match t with
        | Beach  -> TiledRoomRenderer([| Resources.Texture.SAND;  Resources.Texture.SANDSTONE |])
        | Forest -> TiledRoomRenderer([| Resources.Texture.GRASS; Resources.Texture.GRASS_DARK |])
        | Cave   -> TiledRoomRenderer([| Resources.Texture.STONE; Resources.Texture.STONE_DARK |])
        | Snow   -> TiledRoomRenderer([| Resources.Texture.SNOW;  Resources.Texture.SNOW_DARK |])

    /// Maps the server's biome string to a room type (defaults to Beach).
    let ofString (s: string) : RoomType =
        match s with
        | "forest" -> Forest
        | "cave"   -> Cave
        | "snow"   -> Snow
        | _        -> Beach
