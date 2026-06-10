namespace DMIslandClient

open DMIslandClient.Scenes
open LadaEngine

type Game () =
    let window = Window.Create(800, 600, "DM Island: Main Menu")
    let mutable scene = Unchecked.defaultof<IScene>

    let loadScene (newScene: IScene) =
        scene <- newScene
        scene.Load()
        scene.Resize()
        window.Title <- "DM Island: " + newScene.Name

    do loadScene <| MainMenuScene(loadScene, window)

    do
        window.Render.Add(fun () -> scene.Render())
        window.Loaded.Add(fun () -> scene.Load())
        window.FixedUpdate.Add(fun _ -> scene.FixedUpdate())
        window.Update.Add(fun dt -> scene.Update dt)
        window.Resized.Add(fun () -> scene.Resize())

    member x.Run() = window.Run()
