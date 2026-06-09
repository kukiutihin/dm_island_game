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
    let mutable currentBiome = ""
    let mutable currentRoom: Room option = None
    let entities = EntityGroup()
    let effects = EffectGroup()
    let camera = ElasticCamera(Camera())
    let ui = GameUI()
    let deathUI = GameOverUI("You Died!", "Press R to Respawn")
    let winUI = GameOverUI("You Win!", "Press R to play again")
    let sync = SynchroQueue()

    let mutable isDead = false
    let mutable won = false

    let controller = PlayerController(connection)
    let dispatcher = EventDispatcher(entities, effects, ui)

    // Rebuild the floor background when the server reports a different biome.
    let updateRoom (biome: string) =
        let biome = if isNull biome then "" else biome
        if biome <> currentBiome then
            currentBiome <- biome
            currentRoom <- Some (Room(15, 15, RoomRenderer.ofString biome))

    let applyUpdate (event: Dto.GameStateResponse) =
        dispatcher.ProcessUpdate(event)
        updateRoom event.Biome
        won <- event.Completed
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
            deathUI.Load()
            winUI.Load()

        member this.Render() =
            currentRoom |> Option.iter _.Render(camera.GetCamera())
            entities.Render(camera.GetCamera())
            effects.Render(camera.GetCamera())
            ui.Render()
            if won then winUI.Render()
            elif isDead then deathUI.Render()

        member this.Resize() =
            ui.Resize(window)
            deathUI.Resize(window)
            winUI.Resize(window)

        member this.Update(dt: float32) =
            if won || isDead then
                // Game is frozen on the end screen until the player restarts.
                (if won then winUI.Update() else deathUI.Update())
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
