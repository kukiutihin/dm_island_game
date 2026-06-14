namespace DMIslandClient.Scenes

open DMIslandClient.Connection
open DMIslandClient.UI
open LadaEngine

type MainMenuScene(loadScene, window: Window, detachedMode: bool, url) =
    let connection: IGameConnection =
        if detachedMode
        then PingGameConnection(url)
        else GameConnection(url)
    let gameScene = GameScene(connection, window)
    
    let ui = MainMenuUI()
    
    let mutable timeTillNextCheck = 1f
    
    let rec liveCheck alive =
        if not alive then ui.AddAttempt()
        else loadScene gameScene
    
    interface IScene with
        member this.FixedUpdate() = ()
        member this.Name = "Main Menu"
        member this.Load() =
            connection.CheckAlive(liveCheck)
            ui.Load()
            
        member this.Render() =
            ui.Render()
            
        member this.Resize() =
            ui.Resize(window)
            
        member this.Update(dt) =
            timeTillNextCheck <- timeTillNextCheck - dt
            if timeTillNextCheck < 0f then
                timeTillNextCheck <- 1f
                connection.CheckAlive(liveCheck)
            ui.Update()

