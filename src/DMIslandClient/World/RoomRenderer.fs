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
    abstract Update: float32 -> unit
    abstract AddFloorTile: Pos -> unit
    abstract UpdateVertices: unit -> unit


type TiledRoomRenderer(floorTextures: string seq) =
    let atlas = TextureAtlas(floorTextures)
    let floorGroup = SpriteGroup(atlas)
    let addTile (group: SpriteGroup) (textureSet: string seq) (pos: Pos) =
        let texture = GameRandom.choice(textureSet)
        let sprite = Sprite(pos, atlas, texture)
        group.AddSprite(sprite)
        
    member x.GetGroup() = floorGroup

    interface IRoomRenderer with
        member _.Render(camera) =
            floorGroup.Render(camera)
        member _.AddFloorTile(pos) =
            addTile floorGroup floorTextures pos
        member _.Update(dt) = ()
        member _.UpdateVertices() =
            floorGroup.Update()
            
type ShadedTileRoomRenderer(floorTextures: string seq) =
    let mutable time = 0f
    let rawParent = TiledRoomRenderer(floorTextures: string seq)
    let shader = Shader.ofSource RoomRendererShaders.waterVert RoomRendererShaders.waterFrag
    do rawParent.GetGroup().Renderer.Shader <- shader
    let parent: IRoomRenderer = rawParent

    interface IRoomRenderer with
        member _.Render(camera) = parent.Render(camera)
        member _.AddFloorTile(pos) = parent.AddFloorTile(pos)
        member _.Update(dt) =
            time <- time + dt
            Shader.setFloat "time" time shader
            parent.Update(dt)
            
        member _.UpdateVertices() = parent.UpdateVertices()
        
module RoomRenderer =
    let getFor (t: RoomType) : IRoomRenderer =
        match t with
        | Beach -> TiledRoomRenderer([| Resources.Texture.SAND; Resources.Texture.SAND; Resources.Texture.SAND; Resources.Texture.SANDSTONE |])
        | Forest -> TiledRoomRenderer([| Resources.Texture.GRASS; Resources.Texture.GRASS; Resources.Texture.GRASS; Resources.Texture.GRASS_DARK |])
        | Swamp -> ShadedTileRoomRenderer([| Resources.Texture.SWAMP;Resources.Texture.SWAMP; Resources.Texture.SWAMP; Resources.Texture.SWAMP_BLAZE |])
        | NerdForest -> TiledRoomRenderer([| Resources.Texture.LEAVES; Resources.Texture.LEAVES;  Resources.Texture.LEAVES_DARK |])

    /// Maps the server's biome string to a room type (defaults to Beach).
    let ofString (s: string) : RoomType =
        match s with
        | "beach" -> Beach
        | "forest" -> Forest
        | "swamp" -> Swamp
        | "nerd" -> NerdForest
