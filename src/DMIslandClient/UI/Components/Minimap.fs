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
    let textures = [| Resources.Texture.SANDSTONE; Resources.Texture.DIRT; Resources.Entity.STEVE |]
    let atlas = TextureAtlas(textures)
    let group = SpriteGroup(atlas)

    // Top-right anchor (right edge / top) in UI coordinates, and the cell size.
    let mutable anchor = Pos(1f, 1f)
    let mutable cellSize = 0.05f
    let mutable rooms: RoomCellDto list = []

    let addCell (x: float32) (y: float32) (texture: string) =
        let sprite = Sprite(Pos(x, y), atlas, texture)
        sprite.Width <- cellSize * 0.85f
        sprite.Height <- cellSize * 0.85f
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
            for room in rooms do
                let cx = x0 + float32 (room.X - minX) * cellSize
                let cy = anchor.Y - float32 (-minY - room.Y) * cellSize
                let tex = if room.Cleared then Resources.Texture.SANDSTONE else Resources.Texture.DIRT
                addCell cx cy tex
                if room.Current then
                    addCell cx cy Resources.Entity.STEVE
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
