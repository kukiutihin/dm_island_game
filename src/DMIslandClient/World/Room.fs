namespace DMIslandClient.World

open System
open System.Collections.Generic
open LadaEngine

type Room(width: int, height: int, typ: RoomType) =
    let renderer = RoomRenderer.getFor typ

    let () =
        let positions = Seq.init width (fun i -> Seq.init height (fun j -> Pos(i, j))) |> Seq.concat
        Seq.iter renderer.AddTile positions
        renderer.Update()

    member _.Render(camera) = renderer.Render(camera)
    
