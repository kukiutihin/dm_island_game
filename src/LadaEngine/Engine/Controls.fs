namespace LadaEngine

open OpenTK.Windowing.GraphicsLibraryFramework

/// Global input state, refreshed by the window every update frame
module Controls =
    let mutable Mouse = Unchecked.defaultof<MouseState>
    let mutable Keyboard = Unchecked.defaultof<KeyboardState>

    /// Cursor position in screen coordinates
    let mutable CursorPosition = Pos.Zero

    /// WASD movement vector
    let mutable ControlDirection = IntPos.Zero

    /// WASD movement vector (float)
    let mutable ControlDirectionF = Pos.Zero

    /// True only on the frame the key went down
    let keyPressedOnce (key: Keys) = Keyboard.IsKeyPressed key

    /// True every frame the key is held down
    let keyHeld (key: Keys) = Keyboard.IsKeyDown key

    /// True only on the frame the mouse button went down
    let mouseButtonPressedOnce (button: MouseButton) = Mouse.IsButtonPressed button
