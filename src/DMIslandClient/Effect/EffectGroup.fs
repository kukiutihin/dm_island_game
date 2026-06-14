namespace DMIslandClient.Effect

open System
open System.Collections.Generic
open DMIslandClient.Resources
open LadaEngine

type EffectType =
    | EtEntityDeath
    | EtTearPop
    | EtProjectilePop
    | EtMobAttack


type EffectGroup() =
    let textures = [|
        Resources.Particle.SMOKE1
        Resources.Particle.SMOKE2
        Resources.Particle.BUBBLE
        Resources.Particle.ENEMY_PROJECTILE
        Resources.Particle.HIT_SPARK
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
        
    let createProjectileExplosion position =
        let effect = ProjectilePopEffect(position, atlas, spriteGroup)
        effects.Add(effect)

    let createMobAttack position =
        let effect = MobAttackEffect(position, atlas, spriteGroup)
        effects.Add(effect)

    member x.CreateEffect(typ: EffectType, position: Pos) =
        match typ with
        | EtEntityDeath -> createEntityDeath position
        | EtProjectilePop -> createProjectileExplosion position
        | EtTearPop -> createTearExplosion position
        | EtMobAttack -> createMobAttack position
    
    member x.Update(dt: float32) =
        effects |> Seq.iter _.Update(dt)
        effects |> Seq.filter _.Finished() |> Seq.iter _.Destroy()
        effects.RemoveAll(_.Finished()) |> ignore
        spriteGroup.Update()
    
    member x.Render(camera: Camera) =
        spriteGroup.Render(camera)
