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
    // Optional frame-by-frame animation that cycles the sprite's texture.
    let mutable animation : SpriteAnimation option = None

    abstract member SetTarget : Pos -> unit
    abstract member Teleport : Pos -> unit

    member x.Sprite = sprite

    /// Plays a frame animation on this entity. Every frame name must be present in the
    /// atlas backing this entity's sprite group. fps = 0 (or a single frame) is static.
    member public x.SetAnimation(frames: string[], fps: float32, ?looping: bool) =
        animation <- Some(SpriteAnimation(sprite, frames, fps, looping = defaultArg looping true))

    /// Stops and removes the current frame animation (the sprite keeps its last frame).
    member public x.ClearAnimation() = animation <- None

    /// The currently playing frame animation, if any.
    member public x.Animation = animation

    member public x.Update(dt) =
        position.Update(dt)
        scaleControl.Update(dt)
        // Advance frames before the SpriteGroup rebuilds its vertices this frame.
        animation |> Option.iter _.Update(dt)
        x.Sprite.Width <- (sign x.Sprite.Width |> float32) * scaleControl.GetValue().X
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