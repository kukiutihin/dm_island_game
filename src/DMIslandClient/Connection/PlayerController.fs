namespace DMIslandClient.Connection

open DMIslandClient.Connection.Dto
open DMIslandClient.Connection
open LadaEngine
open OpenTK.Windowing.GraphicsLibraryFramework

type PlayerController(connection: IGameConnection) =
    let subscriptions = ResizeArray()
    let mutable onAttack : unit -> unit = ignore
    let mutable onFace : bool -> unit = ignore

    // Steps per held-down move are rate-limited so the character walks at a steady pace
    // instead of moving every frame.
    let moveRepeatInterval = 0.15f
    let mutable moveCooldown = 0f

    // Direction sent while a WASD key is held down (priority order).
    let heldMoveDirection () =
        if Controls.keyHeld(Keys.D) then Some "right"
        else if Controls.keyHeld(Keys.A) then Some "left"
        else if Controls.keyHeld(Keys.S) then Some "up"
        else if Controls.keyHeld(Keys.W) then Some "down"
        else None

    let getShootDirection () =
        if Controls.keyPressedOnce(Keys.Right) then Some "right"
        else if Controls.keyPressedOnce(Keys.Left) then Some "left"
        else if Controls.keyPressedOnce(Keys.Down) then Some "up"
        else if Controls.keyPressedOnce(Keys.Up) then Some "down"
        else None

    let notifyAll result =
        Seq.iter (fun x -> x result) subscriptions

    member _.Update(dt: float32) =
        // Face the side of the most recently pressed horizontal key.
        if Controls.keyPressedOnce(Keys.D) then onFace true
        if Controls.keyPressedOnce(Keys.A) then onFace false

        // Hold-to-move, throttled by the repeat interval.
        if moveCooldown > 0f then moveCooldown <- moveCooldown - dt
        match heldMoveDirection () with
        | Some dir when moveCooldown <= 0f ->
            connection.MoveCallback(dir, notifyAll)
            moveCooldown <- moveRepeatInterval
        | Some _ -> ()
        | None -> moveCooldown <- 0f // released: next press steps immediately

        getShootDirection () |> Option.iter (fun dir ->
            connection.ShootCallback(dir, notifyAll)
            onAttack ())

    /// Called locally the moment the player fires (used to play the attack pose).
    member _.SetOnAttack(callback: unit -> unit) = onAttack <- callback

    /// Called when the player presses a horizontal key (true = right, false = left).
    member _.SetOnFace(callback: bool -> unit) = onFace <- callback

    member _.SubscribeToUpdate(callback: GameStateResponse -> unit) =
        subscriptions.Add(callback)

    member _.SendInitial() = connection.SkibCallback(notifyAll)
