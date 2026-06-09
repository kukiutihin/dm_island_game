namespace DMIslandClient.UI

open DMIslandClient.Resources
open DMIslandClient.UI.Image
open DMIslandClient.UI.Text
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Global

type MainMenuUI () =
    let uiCamera = Camera()
    let gameText = Text("DM Island Game v0.0.1", Pos(0f, 0f))
    let connectionText = Text("Connecting to the server", Pos(0f, 0f))
    let backgroundImage = Image(Resources.UI.ISLAND, Pos(0f, 0f))
    
    let mutable attempts = 0
    let mutable connectionTextDotCount = 0
    
    member x.Render() =
        backgroundImage.Render(uiCamera)
        gameText.Render(uiCamera)
        connectionText.Render(uiCamera)
    
    member x.Update() =
        gameText.Update()

        connectionTextDotCount <- connectionTextDotCount + 1
        connectionText.SetText($"Connecting to the server (attempts:{attempts})" + String.replicate (connectionTextDotCount / 1000 % 3 + 1) ".")
        connectionText.Update()
    
    member x.AddAttempt() =
        attempts <- attempts + 1
    
    member x.Load() =
        ()
    
    member x.Resize(window: Window) =
        let top = float32 window.ClientSize.Y / float32 window.ClientSize.X
        let left = -1f
        let right = 1f
        let bottom = - float32 window.ClientSize.Y / float32 window.ClientSize.X
        let ratio = top / right
        let scaling = top
        gameText.SetScale(0.1f * scaling)
        gameText.SetPosition(Pos(left + 0.05f * scaling, top - ratio * 0.1f))
        
        connectionText.SetScale(0.1f * scaling)
        connectionText.SetPosition(Pos(left + 0.05f * scaling, top - ratio * 0.25f))
        
        backgroundImage.GetSprite().Height <- 2f * ratio
        backgroundImage.GetSprite().Width <- 2f
        backgroundImage.Update()