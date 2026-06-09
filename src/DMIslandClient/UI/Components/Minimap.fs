namespace DMIslandClient.UI.Components

open System.Collections.Generic
open DMIslandClient.Connection.Dto
open DMIslandClient.Resources
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Common.SpriteGroup
open LadaEngine.Engine.Renderables.GroupRendering

/// Isaac-style floor map. Cleared rooms are bright, not-yet-cleared rooms are
/// dark, and the current room is marked. Anchored to the top-right corner.
type Minimap() =
    let textures = [| Resources.UI.ROOM_CLEAR; Resources.UI.ROOM_UNCLEAR; Resources.Entity.STEVE |]
    let atlas = TextureAtlas(textures)
    let group = SpriteGroup(atlas)

    // Top-right anchor (right edge / top) in UI coordinates, and the cell size.
    let mutable anchor = Pos(1f, 1f)
    let mutable cellSize = 0.1f
    let mutable rooms: RoomCellDto list = []

    let addCell (x: float32) (y: float32) (texture: string) =
        let sprite = Sprite(Pos(x, y), atlas, texture)
        sprite.Width <- cellSize * 1f
        sprite.Height <- cellSize * 1f
        group.AddSprite(sprite)

    let addSteve (x: float32) (y: float32) =
        let sprite = Sprite(Pos(x, y), atlas, Resources.Entity.STEVE)
        sprite.Width <- cellSize * 0.6f
        sprite.Height <- cellSize * 0.6f
        group.AddSprite(sprite)
    
    let rebuild () =
        group.Sprites.Clear()
        match rooms with
        | [] -> ()
        | _ ->
            let minX = rooms |> List.map _.X |> List.min
            let minY = rooms |> List.map (fun room -> -room.Y) |> List.min
            let maxX = rooms |> List.map _.X |> List.max
            let cols = maxX - minX + 1
            let x0 = anchor.X - float32 cols * cellSize
            let yScale = 0.75f
            for room in rooms do
                let cx = x0 + float32 (room.X - minX) * cellSize
                let cy = anchor.Y - float32 (-minY - room.Y) * cellSize * yScale
                let tex = if room.Cleared then Resources.UI.ROOM_CLEAR else Resources.UI.ROOM_UNCLEAR
                addCell cx cy tex
                if room.Current then addSteve cx cy
        group.Update()

    member _.SetRooms(newRooms: RoomCellDto seq) =
        rooms <- if isNull (box newRooms) then [] else List.ofSeq newRooms
        rebuild ()

    member _.SetLayout(topRight: Pos, size: float32) =
        anchor <- topRight
        cellSize <- size
        rebuild ()

    member _.Render(camera: Camera) =
        group.Render(camera)
