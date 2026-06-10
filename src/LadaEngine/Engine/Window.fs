namespace LadaEngine

open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open OpenTK.Windowing.Common
open OpenTK.Windowing.Desktop
open OpenTK.Windowing.GraphicsLibraryFramework

/// Game window. Subscribe to the Loaded / Update / FixedUpdate / Render /
/// Resized events to drive the game loop.
type Window(gameWindowSettings: GameWindowSettings, nativeWindowSettings: NativeWindowSettings) =
    inherit GameWindow(gameWindowSettings, nativeWindowSettings)

    /// Refresh rate of FixedUpdate (250 Hz)
    static let fixedUpdateRate = 0.004

    let loadedEvent = Event<unit>()
    let renderEvent = Event<unit>()
    let updateEvent = Event<float32>()
    let fixedUpdateEvent = Event<float32>()
    let resizedEvent = Event<unit>()
    let mutable accumulator = 0.0

    do
        GL.Enable EnableCap.Texture2D
        GL.Enable EnableCap.Blend
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)

    /// Fired once the GL context is ready
    member _.Loaded = loadedEvent.Publish

    /// Fired every rendered frame
    member _.Render = renderEvent.Publish

    /// Fired every update frame with the frame's delta time
    member _.Update = updateEvent.Publish

    /// Fired at a fixed 250 Hz rate
    member _.FixedUpdate = fixedUpdateEvent.Publish

    /// Fired when the window is resized or moved
    member _.Resized = resizedEvent.Publish

    /// Creates a window with the given size and title
    static member Create(width: int, height: int, title: string) =
        let nativeSettings =
            NativeWindowSettings(
                Size = Vector2i(width, height),
                Title = title,
                // Needed to run on macOS
                Flags = ContextFlags.ForwardCompatible)

        new Window(GameWindowSettings.Default, nativeSettings)

    member private this.ResizeAndNotify() =
        let size = this.ClientSize
        GL.Viewport(0, -(size.X - size.Y) / 2, size.X, size.X)

        Misc.ScreenRatio <- float32 this.Size.X / float32 this.Size.Y
        Misc.FboSpriteCoords <- Pos(2f * float32 size.X / float32 size.X - 1f, 2f * float32 size.Y / float32 size.Y - 1f)

        resizedEvent.Trigger()

    override this.OnLoad() =
        base.OnLoad()
        GL.ClearColor(0f, 0f, 0f, 0f)
        loadedEvent.Trigger()

    override this.OnRenderFrame(e: FrameEventArgs) =
        base.OnRenderFrame e
        GL.Clear ClearBufferMask.ColorBufferBit

        renderEvent.Trigger()

        accumulator <- accumulator + e.Time
        while accumulator > fixedUpdateRate do
            fixedUpdateEvent.Trigger(float32 accumulator)
            accumulator <- accumulator - fixedUpdateRate
            if accumulator > 30.0 then accumulator <- 0.0

        this.SwapBuffers()

    override this.OnUpdateFrame(e: FrameEventArgs) =
        base.OnUpdateFrame e

        Controls.Mouse <- this.MouseState
        Controls.Keyboard <- this.KeyboardState

        Controls.CursorPosition <-
            Pos(2f * this.MouseState.X / float32 this.Size.X, 2f * this.MouseState.Y / float32 this.Size.Y)

        let axis (positive: Keys) (negative: Keys) =
            (if this.KeyboardState.IsKeyDown positive then 1 else 0)
            - (if this.KeyboardState.IsKeyDown negative then 1 else 0)

        let dx = axis Keys.D Keys.A
        let dy = axis Keys.W Keys.S
        Controls.ControlDirection <- IntPos(dx, dy)
        Controls.ControlDirectionF <- Pos(dx, dy)

        if this.KeyboardState.IsKeyPressed Keys.F11 then
            if this.WindowBorder <> WindowBorder.Hidden then
                this.WindowBorder <- WindowBorder.Hidden
                this.WindowState <- WindowState.Fullscreen
            else
                this.WindowBorder <- WindowBorder.Resizable
                this.WindowState <- WindowState.Normal

        updateEvent.Trigger(float32 e.Time)

    override this.OnResize(e: ResizeEventArgs) =
        base.OnResize e
        this.ResizeAndNotify()

    override this.OnMove(e: WindowPositionEventArgs) =
        base.OnMove e
        this.ResizeAndNotify()
