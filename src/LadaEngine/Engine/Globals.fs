namespace LadaEngine

open System
open System.Text.RegularExpressions

/// How object coordinates are interpreted by the renderer
type CoordinateMode =
    /// Entire screen is based on a (-1, -1, 1, 1) grid
    | GlBased
    /// Objects have global coordinates, not bound to screen coordinates
    | NonGlBased

module GlobalOptions =
    let mutable CoordinateMode = NonGlBased

/// Internal GL state cache used to minimise redundant state changes
module internal GlState =
    let mutable boundFramebuffer = 0
    let mutable lastShaderUsed = -1
    let lastTextureUsed : int[] = Array.create 16 -1

module Misc =
    /// Vertices for a fullscreen quad (4 corners x [x, y, z, u, v])
    let fullscreenVertices =
        [| 1f; 1f; 0f; 1f; 1f
           1f; -1f; 0f; 1f; 0f
           -1f; -1f; 0f; 0f; 0f
           -1f; 1f; 0f; 0f; 1f |]

    /// Current window width / height ratio, updated on resize
    let mutable ScreenRatio = 0.6f / 0.8f

    /// Width and height of framebuffer sprite to be rendered, updated on resize
    let mutable FboSpriteCoords = Pos(1f, 1f)

    /// Distance between two points
    let len (a: Pos) (b: Pos) = Pos.len a b

    /// Clamps a value to [0, 1]
    let normalize (x: float32) = max 0f (min 1f x)

    let log (message: obj) = Console.WriteLine message

    /// Pretty-prints a shader compilation error, highlighting failing lines
    let printShaderError (shaderSource: string) (error: string) =
        Console.ForegroundColor <- ConsoleColor.Red
        Console.WriteLine "Shader compilation error!\n\n"

        let failingLines =
            error.Split '\n'
            |> Seq.choose (fun line ->
                let m = Regex.Match(line, @"^ERROR:\s*\d+:(\d+)")
                if m.Success then Some (int m.Groups[1].Value - 1) else None)
            |> Set.ofSeq

        shaderSource.Split '\n'
        |> Array.iteri (fun i line ->
            Console.ForegroundColor <- if failingLines.Contains i then ConsoleColor.Red else ConsoleColor.Gray
            Console.WriteLine $"{i + 1} {line}")

        Console.ForegroundColor <- ConsoleColor.Blue
        Console.WriteLine error
        Console.ForegroundColor <- ConsoleColor.White
