namespace DMIslandClient.Effect

open System
open System.Collections.Generic
open DMIslandClient.Resources
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common
open LadaEngine.Engine.Common.SpriteGroup
open LadaEngine.Engine.Renderables.GroupRendering

type EffectType =
    | EtEntityDeath
    | EtTearPop

type EffectGroup() =
    let textures = [|
        Resources.Particle.SMOKE1
        Resources.Particle.SMOKE2
        Resources.Particle.BUBBLE
    |]
    
    let atlas = TextureAtlas(textures)
    let spriteGroup = SpriteGroup(atlas)
    let effects = ResizeArray<Effect>()
    
    let createEntityDeath position =
        let effect = DeathEffect(position, atlas, spriteGroup)
        effects.Add(effect)
    
    let createTearExplosion position =
        let effect = TearPopEffect(position, atlas, spriteGroup)
        effects.Add(effect)
    
    member x.CreateEffect(typ: EffectType, position: Pos) =
        match typ with
        | EtEntityDeath -> createEntityDeath position
        | EtTearPop -> createTearExplosion position
    
    member x.Update(dt: float32) =
        effects |> Seq.iter _.Update(dt)
        effects.RemoveAll(_.Finished()) |> ignore
        spriteGroup.Update()
    
    member x.Render(camera: Camera) =
        spriteGroup.Render(camera)
        
        
        
module T =
    let inline lerp c p t =
        t * c + p * (LanguagePrimitives.GenericOne - c)

    lerp 0.1f 0f 1f |> printfn "%A"
    lerp 0.1f System.Numerics.Vector2.Zero System.Numerics.Vector2.One |> printfn "%A"
    lerp 0.1f System.Numerics.Vector3.Zero System.Numerics.Vector3.One |> printfn "%A"