namespace DMIslandClient

open DMIslandClient.Connection
open DMIslandClient.Scenes
open LadaEngine.Engine.Global
open LadaEngine.Engine.Scene

type Game () =
    let window = Window.Create(800, 600, "DM Island: Main Menu")
    let mutable scene: IScene = null

    let loadScene (newScene: IScene) =
        scene <- newScene
        scene.Load()
        scene.Resize()
        window.Title <- "DM Island: " + newScene.GetName()
        
    do loadScene <| MainMenuScene(loadScene, window)
    
    let () =
        window.add_Render(fun () -> scene.Render())
        window.add_Load(System.Action(fun _ -> scene.Load()))
        window.add_FixedUpdate(fun _ -> scene.FixedUpdate ())
        window.add_Update(fun x -> scene.Update(float32 x))
        window.add_Resize(scene.Resize)
    
    member x.Run() = window.Run()
                 