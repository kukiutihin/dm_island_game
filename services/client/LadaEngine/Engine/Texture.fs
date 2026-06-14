namespace LadaEngine

open OpenTK.Graphics.OpenGL4
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing

/// An OpenGL texture, identified by its GL handle
[<Struct>]
type Texture = { Handle: int }

module Texture =
    let internal setNearestFiltering () =
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, int TextureWrapMode.Repeat)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, int TextureWrapMode.Repeat)

    /// Uploads image pixels (flipped vertically for GL) to the currently bound texture
    let private uploadPixels (bmp: Image<Rgba32>) =
        let pixels = Array.zeroCreate<byte> (bmp.Width * bmp.Height * 4)
        bmp.Mutate(fun ctx -> ctx.Flip FlipMode.Vertical |> ignore)
        bmp.CopyPixelDataTo pixels
        bmp.Mutate(fun ctx -> ctx.Flip FlipMode.Vertical |> ignore)

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            bmp.Width,
            bmp.Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels)

    /// Creates a texture from an in-memory image
    let ofImage (bmp: Image<Rgba32>) : Texture =
        let handle = GL.GenTexture()
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, handle)
        uploadPixels bmp
        setNearestFiltering ()
        GL.GenerateMipmap GenerateMipmapTarget.Texture2D
        { Handle = handle }

    /// Creates a texture from an image file
    let ofFile (path: string) : Texture =
        let handle = GL.GenTexture()
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, handle)
        (
            use bmp = Image.Load<Rgba32> path
            uploadPixels bmp
        )
        setNearestFiltering ()
        { Handle = handle }

    /// Replaces the pixel data of an existing texture
    let update (bmp: Image<Rgba32>) (texture: Texture) =
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, texture.Handle)
        uploadPixels bmp

    /// Binds the texture to the given texture unit (skipped if already bound)
    let bind (texUnit: TextureUnit) (texture: Texture) =
        let slot = int texUnit - int TextureUnit.Texture0
        if GlState.lastTextureUsed[slot] <> texture.Handle then
            GL.ActiveTexture texUnit
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle)
            GlState.lastTextureUsed[slot] <- texture.Handle
