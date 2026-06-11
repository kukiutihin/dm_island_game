namespace DMIslandClient.Scenes

open DMIslandClient
open DMIslandClient.Connection
open DMIslandClient.Effect
open DMIslandClient.Entity
open DMIslandClient.UI
open DMIslandClient.World
open LadaEngine


type GameScene(connection: GameConnection, window: Window) =
    let mutable currentRoom: Room option = None
    let entities = MobGroup()
    let objects = StaticObjectGroup()
    let effects = EffectGroup()
    let camera = ElasticCamera(Camera())
    let ui = GameUI()
    let deathUI = GameOverUI("You Died!", "Press R to Respawn")
    let winUI = GameOverUI("You Win!", "Press R to play again")
    let sync = SynchroQueue()

    let mutable isDead = false
    let mutable won = false

    let controller = PlayerController(connection)
    let dispatcher = EventDispatcher(entities, effects, objects.GetGroup(), ui, camera)

    let updateRoom (event: Dto.GameStateResponse) =
        match currentRoom with
        | Some room when room.Id = event.Room.Id -> ()
        | _ -> 
            let room = Room(event.Room.Id, 13, 9, RoomRenderer.ofString event.Room.Biome)
            currentRoom <- Some room

    let applyUpdate (event: Dto.GameStateResponse) =
        objects.SetBiome(event.Room.Biome)
        dispatcher.ProcessUpdate(event)
        updateRoom event
        isDead <- event.Player.Hp = 0
        won <- event.Completed


    interface IScene with
        member this.FixedUpdate() = ()
        member this.Name = "Gaming"

        member this.Load() =
            controller.SubscribeToUpdate(fun event -> sync.AddEvent(fun () -> applyUpdate event))
            controller.SendInitial()
            camera.GetCamera().Zoom <- 7f
            ui.Load()
            deathUI.Load()
            winUI.Load()

        member this.Render() =
            currentRoom |> Option.iter _.Render(camera.GetCamera())
            objects.GetGroup().Render(camera.GetCamera())
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
                (if won then winUI.Update() else deathUI.Update())
                if Controls.keyPressedOnce(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R) then
                    connection.RestartCallback(fun resp -> sync.AddEvent(fun () -> applyUpdate resp))
            else
                controller.Update()

            sync.ExecuteAll()
            objects.GetGroup().Update(dt)
            entities.Update(dt)
            effects.Update(dt)
            camera.Update(dt)
            ui.Update(dt)
