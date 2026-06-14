namespace DMIslandClient.World

open System
open DMIslandClient.Resources
open LadaEngine

type RoomVignette(width, height) =
    let textures = [| Resources.Texture.SHADE |]
    let atlas = TextureAtlas(textures)
    let group = SpriteGroup(atlas)
    
    let place pos rot =
        let sprite = Sprite(pos, atlas, Resources.Texture.SHADE)
        group.AddSprite(sprite)
        sprite.Rotation <- rot
    
    member x.Build() =
        let pi = MathF.PI
        for i = 0 to width do place (Pos(i, 0)) (pi * 1.5f)
        for i = 0 to height do place (Pos(0, i)) (0f)
        for i = 0 to width do place (Pos(i, height - 1)) (pi * 0.5f)
        for i = 0 to height do place (Pos(width - 1, i)) (pi)
        group.Update()
    
    member x.Render(camera) =
        group.Render(camera)
