namespace DMIslandClient.Entity

open DMIslandClient.Animation.AnimatablePos
open LadaEngine

type ElasticCamera(camera: Camera) =
    let position: IAnimatablePos = SmoothAnimatablePos(10f, Pos(0f, 0f))

    member x.GetCamera() = camera
    member x.SetPosition(target: Pos) = position.SetTarget(target)
    member x.Update(dt) =
        position.Update(dt)
        camera.Position <- position.GetValue()
        
    

