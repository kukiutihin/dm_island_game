namespace DMIslandClient.World

open System
open System.Collections.Generic
open LadaEngine

type Room(id: Guid, width: int, height: int, typ: RoomType) =
    
    let renderer = RoomRenderer.getFor typ
    let vignette = RoomVignette(width, height)
    let () =
        let positions = Seq.init width (fun i -> Seq.init height (fun j -> Pos(i, j))) |> Seq.concat
        Seq.iter renderer.AddFloorTile positions
        renderer.Update()
        vignette.Build()
        
    member _.Render(camera) =
        renderer.Render(camera)
        vignette.Render(camera)
    member _.Id = id

