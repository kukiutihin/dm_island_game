namespace LadaEngine

open System
open System.Globalization
open System.IO
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics

/// Uploads and draws the vertex data of a list of sprites.
/// Supports camera position and zoom via shader uniforms.
type SpriteGroupRenderer(atlas: ITextureAtlas, sprites: ResizeArray<Sprite>) =
    let verts = ResizeArray<float32>()
    let mutable indices: int[] = Array.empty

    let vao = GL.GenVertexArray()
    do GL.BindVertexArray vao

    let vbo = GL.GenBuffer()
    do
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, 0, Array.empty<float32>, BufferUsageHint.DynamicDraw)

    let ebo = GL.GenBuffer()
    do
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
        GL.BufferData(BufferTarget.ElementArrayBuffer, 0, Array.empty<int>, BufferUsageHint.StaticDraw)

    let mutable shader = Shader.ofSource Shaders.cameraVert Shaders.standardFrag

    do
        Shader.activate shader
        let vertexLocation = Shader.attribLocation "aPosition" shader
        GL.EnableVertexAttribArray vertexLocation
        GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 0)

        let texCoordLocation = Shader.attribLocation "aTexCoord" shader
        GL.EnableVertexAttribArray texCoordLocation
        GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 3 * sizeof<float32>)

    /// Shader used for the whole group
    member _.Shader
        with get () = shader
        and set value = shader <- value

    /// Recomputes vertex and index data from the sprites.
    /// Can be costly for many sprites, use carefully.
    member _.UpdateVerts() =
        verts.Clear()
        for sprite in sprites do
            sprite.AppendVerts verts

        let objectCount = verts.Count / 20
        indices <- Array.zeroCreate (6 * objectCount)
        for i in 0 .. objectCount - 1 do
            indices[6 * i] <- 4 * i
            indices[6 * i + 1] <- 4 * i + 1
            indices[6 * i + 2] <- 4 * i + 3
            indices[6 * i + 3] <- 4 * i + 1
            indices[6 * i + 4] <- 4 * i + 2
            indices[6 * i + 5] <- 4 * i + 3

    /// Refreshes the GPU buffers from the current vertex data
    member _.UpdateBuffers() =
        GL.BindVertexArray vao
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Count * sizeof<float32>, verts.ToArray(), BufferUsageHint.DynamicDraw)
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof<int>, indices, BufferUsageHint.StaticDraw)

    /// Draws all sprites, applying the camera's position and zoom
    member _.Render(camera: Camera) =
        shader |> Shader.setVector2 "position" (Vector2(camera.Position.X, camera.Position.Y))
        shader |> Shader.setFloat "zoom" camera.Zoom

        GL.BindVertexArray vao
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)

        atlas.Bind TextureUnit.Texture0
        Shader.activate shader

        GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0)

/// A group of sprites sharing one texture atlas, rendered in a single draw call.
/// Group sprites that never move together and avoid calling Update on large groups.
type SpriteGroup(atlas: ITextureAtlas) =
    let sprites = ResizeArray<Sprite>()
    let renderer = SpriteGroupRenderer(atlas, sprites)

    member _.Atlas = atlas
    member _.Sprites = sprites
    member _.Renderer = renderer

    member _.AddSprite(sprite: Sprite) = sprites.Add sprite

    /// Recounts all vertices and refreshes GPU buffers.
    /// Costly: avoid calling it every frame for big groups.
    member _.Update() =
        renderer.UpdateVerts()
        renderer.UpdateBuffers()

    /// Renders all sprites with the camera's zoom and position applied
    member _.Render(camera: Camera) = renderer.Render camera

module SpriteGroup =
    let private culture = CultureInfo.InvariantCulture

    /// Loads a sprite group from a level file
    let ofFile (fileName: string) (atlas: ITextureAtlas) : SpriteGroup =
        let group = SpriteGroup atlas

        File.ReadAllText fileName
        |> _.Split('\n')
        |> Seq.filter _.StartsWith("|")
        |> Seq.iter (fun line ->
            let fields = line.Replace("|", "").Split(':')
            let sprite = Sprite(Pos(Single.Parse(fields[1], culture), Single.Parse(fields[2], culture)), atlas, fields[0])
            sprite.Rotation <- Single.Parse(fields[3], culture)
            sprite.Group <- Int32.Parse(fields[4], culture)
            sprite.Width <- Single.Parse(fields[5], culture)
            sprite.Height <- Single.Parse(fields[5], culture)
            group.AddSprite sprite)

        group

    /// Saves a sprite group to a level file
    let saveToFile (fileName: string) (group: SpriteGroup) =
        let lines =
            group.Sprites
            |> Seq.map (fun s ->
                let f (value: float32) = value.ToString culture
                $"|{s.TextureName}:{f s.Position.X}:{f s.Position.Y}:{f s.Rotation}:{s.Group.ToString culture}:{f s.Width}:{f s.Height}")

        File.WriteAllText(fileName, "# Level Format 1.0\n" + String.concat "\n" lines + "\n")
