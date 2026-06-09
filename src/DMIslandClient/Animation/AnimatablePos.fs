module DMIslandClient.Animation.AnimatablePos

open DMIslandClient.Animation.IAnimatableT
open DMIslandClient.Utils
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common.SpriteGroup


type IAnimatablePos = IAnimatableT<Pos>

type SmoothAnimatablePos(speed, initial: Pos) =
    let mutable current: Pos = initial
    let mutable target: Pos = initial
    
    interface IAnimatablePos with
        member this.GetValue() = current
        member this.SetTarget(x) = target <- x
        member this.Update(dt) =
            let delta = dt * speed
            current <- current * (1f - delta) + target * delta
        member this.GetTarget() = target

type FunctionAnimatablePos(f: float32 -> Pos -> Pos -> Pos, speed: float32, initial: Pos) =
    inherit FunctionAnimatableT<Pos>(f, speed, initial)


let linear c target initial =
    MathUtils.lerp c initial target

type LinearAnimatablePos(speed: float32, initial: Pos) =
    inherit FunctionAnimatablePos(linear, speed, initial)


let easeOut c target initial =
    MathUtils.lerp (c * c) initial target

type EaseOutAnimatablePos(speed: float32, initial: Pos) =
    inherit FunctionAnimatablePos(easeOut, speed, initial)


let easeBounceOut height c (target: Pos) (initial: Pos) =
    MathUtils.lerp (c * c) initial target + Pos(0f, height * c * (1f - c))

type EaseOutAndBounceAnimatablePos(height: float32, speed: float32, initial: Pos) =
    inherit FunctionAnimatablePos(easeBounceOut height, speed, initial)


type SpriteFlipper(sprite: Sprite) =
    let mutable looksRight = true
    let mutable current = Pos(0, 0)
    
    member x.SetTarget(pos: Pos) =
        let delta = current.X - pos.X
        let looksRightNow = delta < 0f
        if abs delta > 0.01f && (looksRight <> looksRightNow) then
            looksRight <- looksRightNow
            sprite.Width <- -sprite.Width
        current <- pos
