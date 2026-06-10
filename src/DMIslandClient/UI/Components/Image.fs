namespace DMIslandClient.UI.Image

open LadaEngine

type Image(texture: string, position: Pos) =
    let atlas = TextureAtlas([texture])
    let spriteGroup = SpriteGroup(atlas)
    let sprite = Sprite(position, atlas, texture)
    
    do spriteGroup.AddSprite(sprite)
    
    member x.GetSprite() = sprite
    
    member x.Render(camera: Camera) = spriteGroup.Render(camera)
    
    member x.Update() = spriteGroup.Update()
