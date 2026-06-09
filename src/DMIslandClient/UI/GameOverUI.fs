namespace DMIslandClient.UI

open DMIslandClient.UI.Text
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Global

/// Minecraft-style death screen overlay: a big "You Died!" title and a prompt
/// to respawn. Shown on top of the frozen game world when the player dies.
type GameOverUI() =
    let uiCamera = Camera()
    let title = Text("You Died!", Pos(0f, 0f))
    let prompt = Text("Press R to Respawn", Pos(0f, 0f))

    member x.Render() =
        title.Render(uiCamera)
        prompt.Render(uiCamera)

    member x.Update() =
        title.Update()
        prompt.Update()

    member x.Load() =
        ()

    member x.Resize(window: Window) =
        let top = float32 window.ClientSize.Y / float32 window.ClientSize.X
        let right = 1f
        let ratio = top / right
        let scaling = top

        title.SetScale(0.16f * scaling)
        title.SetPosition(Pos(-0.5f * scaling, 0.13f * ratio))

        prompt.SetScale(0.08f * scaling)
        prompt.SetPosition(Pos(-0.5f * scaling, -0.07f * ratio))
