namespace LadaEngine

/// Drives a <see cref="Sprite"/> through an ordered sequence of frame textures over
/// time, producing classic frame-by-frame animation by swapping the sprite's
/// <c>TextureName</c>.
///
/// Requirements / usage:
/// - Every frame name in <paramref name="frames"/> must already be packed into the
///   atlas backing the sprite (the atlas is fixed at construction time).
/// - Call <c>Update dt</c> once per frame, BEFORE the owning <c>SpriteGroup.Update()</c>
///   rebuilds its vertices, so the new frame is picked up the same frame.
///
/// <param name="fps">Frames per second. 0 (or a single frame) leaves the sprite static.</param>
/// <param name="looping">When true (default) the animation restarts after the last
/// frame; when false it stops on the last frame and reports <c>Finished</c>.</param>
type SpriteAnimation(sprite: Sprite, frames: string[], fps: float32, ?looping: bool) =
    let looping = defaultArg looping true
    // Seconds each frame is shown. Infinite when fps <= 0 so the animation never advances.
    let frameDuration = if fps <= 0f then infinityf else 1f / fps

    let mutable elapsed = 0f
    let mutable index = 0
    let mutable finished = frames.Length = 0
    let mutable playing = true

    do if frames.Length > 0 then sprite.TextureName <- frames.[0]

    /// The frame index currently displayed.
    member _.CurrentFrame = index
    /// True for a non-looping animation that has reached (and stopped on) its last frame.
    member _.Finished = finished
    member _.IsPlaying = playing
    member _.FrameCount = frames.Length

    member _.Play() = playing <- true
    member _.Pause() = playing <- false

    /// Restarts playback from the first frame.
    member _.Restart() =
        elapsed <- 0f
        index <- 0
        finished <- frames.Length = 0
        playing <- true
        if frames.Length > 0 then sprite.TextureName <- frames.[0]

    /// Advances the animation by <paramref name="dt"/> seconds and updates the sprite's texture.
    member _.Update(dt: float32) =
        if not playing || finished || frames.Length <= 1 then
            ()
        else
            elapsed <- elapsed + dt
            while elapsed >= frameDuration do
                elapsed <- elapsed - frameDuration
                if index + 1 < frames.Length then
                    index <- index + 1
                elif looping then
                    index <- 0
                else
                    finished <- true
                    index <- frames.Length - 1
                    elapsed <- 0f // stop the loop on the last frame
            sprite.TextureName <- frames.[index]
