namespace LadaEngine

open System.Globalization

/// 2D float position / vector. Immutable value type.
[<Struct>]
type Pos(x: float32, y: float32) =
    member _.X = x
    member _.Y = y

    new(x: int, y: int) = Pos(float32 x, float32 y)
    new(x: float, y: float) = Pos(float32 x, float32 y)

    static member Zero = Pos(0f, 0f)

    static member (+) (a: Pos, b: Pos) = Pos(a.X + b.X, a.Y + b.Y)
    static member (-) (a: Pos, b: Pos) = Pos(a.X - b.X, a.Y - b.Y)
    static member (*) (a: Pos, k: float32) = Pos(a.X * k, a.Y * k)
    static member (*) (k: float32, a: Pos) = Pos(a.X * k, a.Y * k)

    override this.ToString() =
        this.X.ToString(CultureInfo.InvariantCulture) + " " + this.Y.ToString(CultureInfo.InvariantCulture)

/// 2D integer position. Immutable value type.
[<Struct>]
type IntPos(x: int, y: int) =
    member _.X = x
    member _.Y = y

    static member Zero = IntPos(0, 0)

    static member (+) (a: IntPos, b: IntPos) = IntPos(a.X + b.X, a.Y + b.Y)
    static member (-) (a: IntPos, b: IntPos) = IntPos(a.X - b.X, a.Y - b.Y)
    static member (*) (a: IntPos, k: int) = IntPos(a.X * k, a.Y * k)

    override this.ToString() = $"{this.X} {this.Y}"

module Pos =
    /// Distance between two points
    let len (a: Pos) (b: Pos) =
        sqrt ((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))

    let ofIntPos (p: IntPos) = Pos(p.X, p.Y)

    /// Component-wise floor conversion
    let toIntPos (p: Pos) = IntPos(int (floor p.X), int (floor p.Y))

/// Line segment between two points
[<Struct>]
type Line =
    { First: Pos
      Second: Pos }

    override this.ToString() = $"Segment: A({this.First}), B({this.Second})"

module Line =
    let create first second = { First = first; Second = second }

    /// Intersection point of two segments, if any
    let intersection (a: Line) (b: Line) : Pos option =
        let deltaACy = float (a.First.Y - b.First.Y)
        let deltaDCx = float (b.Second.X - b.First.X)
        let deltaACx = float (a.First.X - b.First.X)
        let deltaDCy = float (b.Second.Y - b.First.Y)
        let deltaBAx = float (a.Second.X - a.First.X)
        let deltaBAy = float (a.Second.Y - a.First.Y)

        let denominator = deltaBAx * deltaDCy - deltaBAy * deltaDCx
        let numerator = deltaACy * deltaDCx - deltaACx * deltaDCy

        if denominator = 0.0 then
            if numerator = 0.0 then
                // Collinear: return one of the potentially infinite intersection points
                if a.First.X >= b.First.X && a.First.X <= b.Second.X then Some a.First
                elif b.First.X >= a.First.X && b.First.X <= a.Second.X then Some b.First
                else None
            else None // parallel
        else
            let r = numerator / denominator
            if r < 0.0 || r > 1.0 then None
            else
                let s = (deltaACy * deltaBAx - deltaACx * deltaBAy) / denominator
                if s < 0.0 || s > 1.0 then None
                else Some (Pos(a.First.X + float32 (r * deltaBAx), a.First.Y + float32 (r * deltaBAy)))
