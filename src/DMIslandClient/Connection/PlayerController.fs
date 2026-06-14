namespace DMIslandClient.Connection

open DMIslandClient.Connection.Dto
open DMIslandClient.Connection
open LadaEngine
open OpenTK.Windowing.GraphicsLibraryFramework

type PlayerController(connection: GameConnection) =
    let subscriptions = ResizeArray()
    let mutable onAttack : unit -> unit = ignore

    let getMoveDirection () =
        if Controls.keyPressedOnce(Keys.D) then Some "right"
        else if Controls.keyPressedOnce(Keys.A) then Some "left"
        else if Controls.keyPressedOnce(Keys.S) then Some "up"
        else if Controls.keyPressedOnce(Keys.W) then Some "down"
        else None
        
    let getShootDirection () =
        if Controls.keyPressedOnce(Keys.Right) then Some "right"
        else if Controls.keyPressedOnce(Keys.Left) then Some "left"
        else if Controls.keyPressedOnce(Keys.Down) then Some "up"
        else if Controls.keyPressedOnce(Keys.Up) then Some "down"
        else None
        
    let notifyAll result =
        Seq.iter (fun x -> x result) subscriptions
        
    member _.Update() =
        getMoveDirection () |> Option.iter (fun dir -> connection.MoveCallback(dir, notifyAll))
        getShootDirection () |> Option.iter (fun dir ->
            connection.ShootCallback(dir, notifyAll)
            onAttack ())

    /// Called locally the moment the player fires (used to play the attack pose).
    member _.SetOnAttack(callback: unit -> unit) = onAttack <- callback

    member _.SubscribeToUpdate(callback: GameStateResponse -> unit) =
        subscriptions.Add(callback)

    member _.SendInitial() = connection.SkibCallback(notifyAll)
