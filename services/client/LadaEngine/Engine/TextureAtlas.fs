namespace LadaEngine

open System.Collections.Generic
open OpenTK.Graphics.OpenGL4
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

/// Stores one or more images in a single GL texture
type ITextureAtlas =
    /// Binds the atlas texture to a GL texture unit
    abstract Bind: TextureUnit -> unit
    /// Texture coordinates (4 corners x [u, v]) of a named image
    abstract GetCoords: name: string -> float32[]

/// Texture atlas built by packing several image files side by side
type TextureAtlas(fileNames: string seq) =
    let files = Seq.toArray fileNames
    let coords = Dictionary<string, float32[]>()

    /// Packs all images horizontally into one image, recording per-file UV coords
    let buildAtlasImage () =
        let images = files |> Array.map Image.Load<Rgba32>
        let height = images |> Array.map (fun i -> i.Height) |> Array.max
        let width = images |> Array.sumBy (fun i -> i.Width)
        let pixels = Array.zeroCreate<byte> (width * height * 4)

        let mutable offset = 0
        (files, images)
        ||> Array.iter2 (fun file image ->
            let source = Array.zeroCreate<byte> (image.Width * image.Height * 4)
            image.CopyPixelDataTo source
            for y in 0 .. image.Height - 1 do
                for x in 0 .. image.Width - 1 do
                    for c in 0 .. 3 do
                        pixels[y * width * 4 + (offset + x) * 4 + c] <- source[y * image.Width * 4 + x * 4 + c]

            coords[file] <-
                [| float32 (offset + image.Width) / float32 width; 0f
                   float32 (offset + image.Width) / float32 width; float32 image.Height / float32 height
                   float32 offset / float32 width; float32 image.Height / float32 height
                   float32 offset / float32 width; 0f |]

            offset <- offset + image.Width
            image.Dispose())
        
        let image = Image.LoadPixelData<Rgba32>(pixels, width, height)
        if (image.Width > 16000) then
            image.Save("faulty.png")
        image

    let handle =
        use atlas = buildAtlasImage ()
        let handle = GL.GenTexture()
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, handle)

        let pixels = Array.zeroCreate<byte> (atlas.Width * atlas.Height * 4)
        atlas.CopyPixelDataTo pixels
        GL.TexImage2D(
            TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, atlas.Width, atlas.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels)

        Texture.setNearestFiltering ()
        GL.GenerateMipmap GenerateMipmapTarget.Texture2D
        handle

    /// OpenGL texture handle of the packed atlas
    member _.Handle = handle

    interface ITextureAtlas with
        member _.Bind(texUnit) = Texture.bind texUnit { Handle = handle }
        member _.GetCoords(name) = coords[name]

/// Atlas backed by a single texture: every name maps to the full texture
type SingleTextureAtlas(texture: Texture) =
    member _.Handle = texture.Handle

    interface ITextureAtlas with
        member _.Bind(texUnit) = Texture.bind texUnit texture

        member _.GetCoords(_) =
            [| 1f; 1f
               1f; 0f
               0f; 0f
               0f; 1f |]
