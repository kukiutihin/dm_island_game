namespace DMIslandClient.Scenes

open DMIslandClient
open DMIslandClient.Connection
open DMIslandClient.Effect
open DMIslandClient.Entity
open DMIslandClient.UI
open DMIslandClient.World
open LadaEngine.Engine.Common
open LadaEngine.Engine.Global
open LadaEngine.Engine.Scene


type GameScene(connection: GameConnection, window: Window) =
    let mutable currentRoom: Room option = Some (Room(20, 20, RoomType.MonadicBeach))
    let entities = EntityGroup()
    let effects = EffectGroup()
    let camera = ElasticCamera(Camera())
    let ui = GameUI()
    let gameOver = GameOverUI()
    let sync = SynchroQueue()

    let mutable isDead = false

    let controller = PlayerController(connection)
    let dispatcher = EventDispatcher(entities, effects, ui)

    let applyUpdate (event: Dto.GameStateResponse) =
        dispatcher.ProcessUpdate(event)
        // The player is dead once the server reports no health left.
        if box event.Player <> null then
            isDead <- event.Player.Hp <= 0

    let trySnapToPlayer () =
        match entities.GetPlayer() with
        | Some player -> camera.SetPosition(player.Position.GetValue())
        | None -> ()

    interface IScene with
        member this.FixedUpdate() = ()
        member this.GetName() = "Gaming"

        member this.Load() =
            controller.SubscribeToUpdate(fun event -> sync.AddEvent(fun () -> applyUpdate event))
            controller.SendInitial()
            camera.GetCamera().Zoom <- 6f
            ui.Load()
            gameOver.Load()

        member this.Render() =
            currentRoom |> Option.iter _.Render(camera.GetCamera())
            entities.Render(camera.GetCamera())
            effects.Render(camera.GetCamera())
            ui.Render()
            if isDead then gameOver.Render()

        member this.Resize() =
            ui.Resize(window)
            gameOver.Resize(window)

        member this.Update(dt: float32) =
            if isDead then
                // Game is frozen on the death screen until the player respawns.
                gameOver.Update()
                if Controls.ButtonPressedOnce(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R) then
                    connection.RestartCallback(fun resp -> sync.AddEvent(fun () -> applyUpdate resp))
            else
                controller.Update()

            trySnapToPlayer()
            sync.ExecuteAll()
            entities.Update(dt)
            effects.Update(dt)
            camera.Update(dt)
            ui.Update(dt)
