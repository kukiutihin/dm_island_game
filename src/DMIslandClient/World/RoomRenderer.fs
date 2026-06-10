namespace DMIslandClient.World

open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine

type RoomType =
    | Beach
    | Forest
    | NerdForest
    | Swamp

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
        | Beach  -> TiledRoomRenderer([| Resources.Texture.SAND; Resources.Texture.SAND; Resources.Texture.SAND; Resources.Texture.SANDSTONE |])
        | Forest -> TiledRoomRenderer([| Resources.Texture.GRASS; Resources.Texture.GRASS; Resources.Texture.GRASS; Resources.Texture.GRASS_DARK |])
        | Swamp   -> TiledRoomRenderer([| Resources.Texture.SWAMP;Resources.Texture.SWAMP; Resources.Texture.SWAMP; Resources.Texture.SWAMP_BLAZE |])
        | Snow   -> TiledRoomRenderer([| Resources.Texture.SNOW;  Resources.Texture.SNOW_DARK |])

    /// Maps the server's biome string to a room type (defaults to Beach).
    let ofString (s: string) : RoomType =
        match s with
        | "beach" -> Beach
        | "forest" -> Forest
        | "swamp" -> Swamp
        | "nerds" -> NerdForest
