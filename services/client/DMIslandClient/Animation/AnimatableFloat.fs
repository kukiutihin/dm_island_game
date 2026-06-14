module DMIslandClient.Animation.AnimatableFloat

open DMIslandClient.Animation.IAnimatableT
open DMIslandClient.Utils


let linear c target initial =
    MathUtils.lerp c initial target

type LinearAnimatableFloat(speed: float32, initial: float32) =
    inherit FunctionAnimatableT<float32>(linear, speed, initial)