namespace DMIslandClient.Entity

open DMIslandClient.Animation.AnimatablePos
open LadaEngine

type ElasticCamera(camera: Camera) =
    let position: IAnimatablePos = SmoothAnimatablePos(5f, Pos(0f, 0f))

    member x.GetCamera() = camera
    member x.SetPosition(target: Pos) =
        let previousTarget = position.GetTarget()
        if Pos.len target previousTarget > 5f then
            position.Teleport(target)
        position.SetTarget(target)

    member x.Update(dt) =
        position.Update(dt)
        camera.Position <- position.GetValue()
        
    

