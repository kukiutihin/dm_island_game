namespace DMIslandClient.UI.Image

open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Common.SpriteGroup
open LadaEngine.Engine.Renderables.GroupRendering

type Image(texture: string, position: Pos) =
    let atlas = TextureAtlas([texture])
    let spriteGroup = SpriteGroup(atlas)
    let sprite = Sprite(position, atlas, texture)
    
    do spriteGroup.AddSprite(sprite)
    
    member x.GetSprite() = sprite
    
    member x.Render(camera: Camera) = spriteGroup.Render(camera)
    
    member x.Update() = spriteGroup.Update()