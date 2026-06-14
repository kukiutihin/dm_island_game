namespace DMIslandClient.Entity

open DMIslandClient.Animation.AnimatablePos
open LadaEngine

type ElasticCamera(camera: Camera) =
    let position: IAnimatablePos = SmoothAnimatablePos(5f, Pos(0f, 0f))
    let rng = System.Random()
    // Current shake magnitude (in tiles); decays to 0 over time.
    let mutable shake = 0f

    member x.GetCamera() = camera
    member x.SetPosition(target: Pos) =
        let previousTarget = position.GetTarget()
        if Pos.len target previousTarget > 5f then
            position.Teleport(target)
        position.SetTarget(target)

    /// Kicks the camera with a brief shake. Larger amount = stronger jolt.
    member x.Shake(amount: float32) =
        shake <- max shake amount

    member x.Update(dt) =
        position.Update(dt)

        // Decay the shake and apply a random offset while it's active.
        shake <- max 0f (shake - dt * 2.0f)
        let offset =
            if shake > 0.001f then
                Pos((rng.NextSingle() - 0.5f) * 2f * shake, (rng.NextSingle() - 0.5f) * 2f * shake)
            else
                Pos(0f, 0f)

        camera.Position <- position.GetValue() + offset
        
    

