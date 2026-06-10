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
    abstract AddFloorTile: Pos -> unit


type TiledRoomRenderer(floorTextures: string seq) =
    let atlas = TextureAtlas(floorTextures)
    let floorGroup = SpriteGroup(atlas)

    let addTile (group: SpriteGroup) (textureSet: string seq) (pos: Pos) =
        let texture = GameRandom.choice(textureSet)
        let sprite = Sprite(pos, atlas, texture)
        group.AddSprite(sprite)

    interface IRoomRenderer with
        member _.Render(camera) =
            floorGroup.Render(camera)
        member _.AddFloorTile(pos) =
            addTile floorGroup floorTextures pos
        member _.Update() =
            floorGroup.Update()


module RoomRenderer =
    let getFor (t: RoomType) : IRoomRenderer =
        match t with
        | Beach  -> TiledRoomRenderer([| Resources.Texture.SAND; Resources.Texture.SANDSTONE |])
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
