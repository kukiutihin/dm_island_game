namespace LadaEngine

open OpenTK.Graphics.OpenGL4

/// An off-screen render target.
/// Load it, call Start to begin capturing draws, Stop to finish,
/// then Render to draw its contents to the screen.
type FrameBuffer() =
    let blankCamera = Camera()
    let mutable fbo = 0
    let mutable contents: (SpriteGroup * Sprite * Texture) option = None

    let createTexture (resolution: IntPos) : Texture =
        let handle = GL.GenTexture()
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, handle)

        GL.TexImage2D(
            TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, resolution.X, resolution.Y,
            0, PixelFormat.Bgra, PixelType.UnsignedByte, nativeint 0)

        Texture.setNearestFiltering ()
        { Handle = handle }

    let loaded () =
        match contents with
        | Some c -> c
        | None -> failwith "Framebuffer must be loaded first"

    /// Texture the framebuffer renders into
    member _.Texture = let _, _, texture = loaded () in texture

    /// Fullscreen sprite displaying the framebuffer texture
    member _.Sprite = let _, sprite, _ = loaded () in sprite

    /// Replaces the shader used to draw the framebuffer contents
    member _.SetShader(shader: Shader) =
        let group, _, _ = loaded ()
        group.Renderer.Shader <- shader

    /// Creates the framebuffer object and its backing texture
    member _.Load(screenResolution: IntPos) =
        fbo <- GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)

        let texture = createTexture screenResolution
        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, texture.Handle, 0)

        let group = SpriteGroup(SingleTextureAtlas texture)
        let sprite = Sprite(Pos.Zero, group.Atlas, "")
        sprite.Width <- 2f
        sprite.Height <- 2f
        group.AddSprite sprite

        contents <- Some (group, sprite, texture)

    /// Starts capturing draws into the framebuffer
    member _.Start() =
        if GlState.boundFramebuffer <> fbo then
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
        GlState.boundFramebuffer <- fbo

    /// Stops capturing, returning to the default framebuffer
    member _.Stop() =
        if GlState.boundFramebuffer <> 0 then
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            GL.Clear ClearBufferMask.ColorBufferBit
        GlState.boundFramebuffer <- 0

    /// Resizes the backing texture
    member this.Resize(screenResolution: IntPos) =
        let _, _, texture = loaded ()
        GL.ActiveTexture TextureUnit.Texture0
        GL.BindTexture(TextureTarget.Texture2D, texture.Handle)

        GL.TexImage2D(
            TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, screenResolution.X, screenResolution.Y,
            0, PixelFormat.Bgra, PixelType.UnsignedByte, nativeint 0)

    /// Draws the framebuffer contents to the screen
    member _.Render() =
        let group, _, _ = loaded ()
        group.Update()
        group.Render blankCamera
