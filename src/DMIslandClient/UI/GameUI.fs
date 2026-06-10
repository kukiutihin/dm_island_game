namespace DMIslandClient.UI

open System.Collections.Generic
open DMIslandClient.Connection.Dto
open DMIslandClient.UI.Components
open DMIslandClient.UI.Text
open LadaEngine

type GameUI() =
    let uiCamera = Camera()
    let healthBar = HealthBar(3)
    let floorText = Text("Floor 1", Pos(0f, 0f))
    let minimap = Minimap()
    let mutable frame = 0

    member x.SetHealth(health) =
        healthBar.UpdateHealth(health)
        
    member x.SetMaxHealth(health) =
        healthBar.UpdateMaxHealth(health)

    member x.SetFloor(n: int) =
        floorText.SetText($"Floor {n}")

    member x.SetMinimap(rooms: IEnumerable<RoomCellDto>) =
        minimap.SetRooms(rooms)

    member x.Update(dt: float32) =
        floorText.Update()

    member x.Render() =
        frame <- frame + 1
        healthBar.Render(uiCamera)
        floorText.Render(uiCamera)
        minimap.Render(uiCamera)

    member x.Load() =
        ()

    member x.Resize(window: Window) =
        let top = float32 window.ClientSize.Y / float32 window.ClientSize.X
        let left = -1f
        let right = 1f
        let ratio = top / right
        let scaling = top

        // Floor number — top-left.
        floorText.SetScale(0.09f * scaling)
        floorText.SetPosition(Pos(left + 0.04f * scaling, top - ratio * 0.20f))

        // Health bar — top-left, below the floor label.
        healthBar.SetScale(0.1f * scaling)
        healthBar.SetPosition(Pos(left + 0.08f * scaling, top - ratio * 0.08f))

        // Minimap — top-right corner.
        minimap.SetLayout(Pos(right - 0.05f * scaling, top - ratio * 0.08f), 0.2f * scaling)
