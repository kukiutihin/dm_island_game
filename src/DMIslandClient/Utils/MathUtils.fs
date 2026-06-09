namespace DMIslandClient.Utils

open LadaEngine.Engine.Base

module MathUtils =
    let inline lerp<^a, ^b
        when (^a or ^b) : (static member ( * ) : ^a * ^b -> ^a)
        and ^a : (static member (+) : ^a * ^a -> ^a)
        and ^b : (static member (-) : ^b * ^b -> ^b)
        and ^b : (static member One : ^b)
    > (c: ^b) (p: ^a) (t: ^a): ^a =
        let f : ^a = t * c
        let g : ^a = p * (LanguagePrimitives.GenericOne - c)
        f + g

    let clamp (i: float32) (lower: float32) (upper: float32) =
        max lower (min upper i)