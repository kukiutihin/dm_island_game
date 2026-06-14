namespace DMIslandClient.UI.Components

open System.Collections.Generic
open DMIslandClient.UI.Text

type FpsCounter(origin) =
    let mutable frames = 0
    let mutable untilUpdate = 0.2f
    
    let text = Text("FPS: ...", origin)
    
    member x.Text() = text
    
    member x.Render(camera) =
        frames <- frames + 1
        text.Render(camera)
    
    member x.Update(dt: float32) =
        untilUpdate <- untilUpdate - dt
        if untilUpdate < 0f then
            untilUpdate <- 0.2f
            text.SetText($"FPS: {5f * float32 frames}")
            text.Update()
            frames <- 0

        