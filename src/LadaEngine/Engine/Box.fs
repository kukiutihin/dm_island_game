namespace LadaEngine

/// Axis-aligned box, used for collision checks
[<Struct>]
type Box =
    { Center: Pos
      Width: float32
      Height: float32 }

module Box =
    let create center width height =
        { Center = center; Width = width; Height = height }

    /// True if the two boxes overlap
    let intersects (a: Box) (b: Box) =
        a.Center.X + a.Width / 2f >= b.Center.X - b.Width / 2f
        && a.Center.X - a.Width / 2f <= b.Center.X + b.Width / 2f
        && a.Center.Y + a.Height / 2f >= b.Center.Y - b.Height / 2f
        && a.Center.Y - a.Height / 2f <= b.Center.Y + b.Height / 2f
