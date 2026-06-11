namespace DMIslandClient.Entity

open DMIslandClient.Animation
open DMIslandClient.Animation.AnimatableFloat
open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Animation.IAnimatableT
open LadaEngine

type Entity (sprite: Sprite, position: IAnimatablePos, scale: float32) =
    let spriteFlipper = SpriteFlipper(sprite)
    let scaleControl: IAnimatablePos = LinearAnimatablePos(10f, Pos(scale, scale))
    let mutable flipping = false
    
    member x.Sprite = sprite
    member x.Position = position
    
    member public x.Update(dt) =
        x.Position.Update(dt)
        scaleControl.Update(dt)
        x.Sprite.Width <- scaleControl.GetValue().X
        x.Sprite.Height <- scaleControl.GetValue().Y
        x.Sprite.Position <- x.Position.GetValue()

    member public x.SetTarget(t: Pos) =
        x.Position.SetTarget(t)
        if flipping then spriteFlipper.SetTarget(t)
    
    member public x.SetFlip(b) = flipping <- b
    
    member public x.SetScale(width: float32, height: float32) = scaleControl.SetTarget(Pos(width, height))
