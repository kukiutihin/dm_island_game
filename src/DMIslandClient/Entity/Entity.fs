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
    
    abstract member SetTarget : Pos -> unit
    abstract member Teleport : Pos -> unit
    
    member x.Sprite = sprite
    
    member public x.Update(dt) =
        position.Update(dt)
        scaleControl.Update(dt)
        x.Sprite.Width <- scaleControl.GetValue().X
        x.Sprite.Height <- scaleControl.GetValue().Y
        x.Sprite.Position <- position.GetValue()

    default x.SetTarget(t: Pos) =
        position.SetTarget(t)
        if flipping then spriteFlipper.SetTarget(t)
        
    member public x.GetTarget() = position.GetTarget()
    default x.Teleport(pos: Pos) = position.Teleport(pos: Pos)
    
    member public x.SetFlip(b) = flipping <- b
    
    member public x.SetScale(width: float32, height: float32) = scaleControl.SetTarget(Pos(width, height))
    
type ImmovableEntity (sprite: Sprite, position: IAnimatablePos, scale: float32) =
    inherit Entity(sprite, position, scale)
    override x.SetTarget(_: Pos) = ()
    override x.Teleport(_: Pos) = ()