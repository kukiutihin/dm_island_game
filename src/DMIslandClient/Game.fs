namespace DMIslandClient

open Argu
open DMIslandClient.Scenes
open LadaEngine

type CliArguments =
    | Observe_Mode
    | Server_Url of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Observe_Mode -> "Just observe the changing game state"
            | Server_Url _ -> "Override the server url. Default is http://localhost:5229"


type Game (args: CliArguments seq) =
    let window = Window.Create(800, 600, "DM Island: Main Menu")
    let mutable scene = Unchecked.defaultof<IScene>

    let loadScene (newScene: IScene) =
        scene <- newScene
        scene.Load()
        scene.Resize()
        window.Title <- "DM Island: " + newScene.Name

    let url =
        Seq.choose (function Server_Url x -> Some x | _ -> None) args
        |> Seq.tryHead
        |> Option.defaultValue "http://localhost:5229"
    
    do loadScene <| MainMenuScene(
        loadScene,
        window,
        Seq.contains Observe_Mode args,
        url
    )

    do
        window.Render.Add(fun () -> scene.Render())
        window.Loaded.Add(fun () -> scene.Load())
        window.FixedUpdate.Add(fun _ -> scene.FixedUpdate())
        window.Update.Add(fun dt -> scene.Update dt)
        window.Resized.Add(fun () -> scene.Resize())

    member x.Run() = window.Run()
