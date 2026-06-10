namespace LadaEngine

/// Camera with position and zoom, applied on SpriteGroup render via shader uniforms
type Camera(position: Pos, zoom: float32) =
    member val Position = position with get, set
    member val Zoom = zoom with get, set

    /// Camera at (0, 0) with zoom 1
    new() = Camera(Pos.Zero, 1f)
