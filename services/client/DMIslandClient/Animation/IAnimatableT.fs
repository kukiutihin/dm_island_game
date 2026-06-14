module DMIslandClient.Animation.IAnimatableT

open DMIslandClient.Utils

type IAnimatableT<'a> =
    abstract SetTarget: 'a -> unit
    abstract GetValue: unit -> 'a
    abstract Update: float32 -> unit
    abstract GetTarget: unit -> 'a
    abstract Teleport: 'a -> unit

type FunctionAnimatableT<'a>(f: float32 -> 'a -> 'a -> 'a, speed: float32, initial: 'a) =
    let mutable initial: 'a = initial
    let mutable target: 'a = initial
    let mutable timeLeft: float32 = 0f
    
    interface IAnimatableT<'a> with
        member this.GetValue() =
            let clamped = MathUtils.clamp timeLeft 0f 1f
            f clamped initial target

        member this.SetTarget(x) =
            let t : IAnimatableT<'a> = this
            initial <- t.GetValue()
            timeLeft <- 1f
            target <- x
        
        member this.Update(dt) =
            if (timeLeft <= 0f) then
                timeLeft <- 0f
                initial <- target
            else timeLeft <- timeLeft - speed * dt

        member this.GetTarget() = target
        
        member this.Teleport(x) =
            target <- x
            initial <- x