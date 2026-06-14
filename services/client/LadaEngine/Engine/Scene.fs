namespace LadaEngine

/// A game scene with lifecycle hooks wired to the window
type IScene =
    /// Name of the scene, often used for the window title
    abstract Name: string
    abstract Load: unit -> unit
    abstract Render: unit -> unit
    abstract Update: dt: float32 -> unit
    abstract FixedUpdate: unit -> unit
    abstract Resize: unit -> unit
