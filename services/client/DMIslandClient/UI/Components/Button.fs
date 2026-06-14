namespace DMIslandClient.UI.Components

open DMIslandClient.UI.Text
open LadaEngine

type Button(text: string, origin: Pos, onClick: unit -> unit) =
    let textSprite = Text(text, origin)
    
    member x.Render(camera) =
        textSprite.Render(camera)
    
    member x.Update() =
        textSprite.Update()
