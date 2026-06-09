namespace DMIslandClient.Connection

open DMIslandClient.Connection.Dto
open DMIslandClient.Connection
open LadaEngine.Engine.Global
open OpenTK.Windowing.GraphicsLibraryFramework

type PlayerController(connection: GameConnection) =
    let subscriptions = ResizeArray()
    
    let getMoveDirection () =
        if Controls.ButtonPressedOnce(Keys.D) then Some "right"
        else if Controls.ButtonPressedOnce(Keys.A) then Some "left"
        else if Controls.ButtonPressedOnce(Keys.S) then Some "up"
        else if Controls.ButtonPressedOnce(Keys.W) then Some "down"
        else None
        
    let getShootDirection () =
        if Controls.ButtonPressedOnce(Keys.Right) then Some "right"
        else if Controls.ButtonPressedOnce(Keys.Left) then Some "left"
        else if Controls.ButtonPressedOnce(Keys.Down) then Some "up"
        else if Controls.ButtonPressedOnce(Keys.Up) then Some "down"
        else None
        
    let notifyAll result =
        Seq.iter (fun x -> x result) subscriptions
        
    member _.Update() =
        getMoveDirection () |> Option.iter (fun dir -> connection.MoveCallback(dir, notifyAll))
        getShootDirection () |> Option.iter (fun dir -> connection.ShootCallback(dir, notifyAll))

    member _.SubscribeToUpdate(callback: GameStateResponse -> unit) =
        subscriptions.Add(callback)

    member _.SendInitial() = connection.SkibCallback(notifyAll)