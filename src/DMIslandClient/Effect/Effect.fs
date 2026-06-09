namespace DMIslandClient.Effect

open DMIslandClient.Animation.AnimatableFloat
open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Animation.IAnimatableT
open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine.Engine.Base
open LadaEngine.Engine.Common.SpriteGroup
open LadaEngine.Engine.Renderables.GroupRendering

type EffectParticle(scale, position: IAnimatablePos, sprite: Sprite) =
    let size: IAnimatableT<float32> = LinearAnimatableFloat(1.6f + GameRandom.random.NextSingle(), scale)
    
    do size.SetTarget(0f)
    
    member public x.Update(dt: float32) =
        sprite.Position <- position.GetValue()
        sprite.Height <- size.GetValue()
        sprite.Width <- size.GetValue()
        position.Update(dt)
        size.Update(dt)
    
    member public x.Finished() = size.GetValue() < 0.03f
    
    member public x.GetSprite() = sprite

type Effect =
    abstract Update: float32 -> unit
    abstract Finished: unit -> bool
    abstract Destroy: unit -> unit

type ExplosionEffect(textures, scale, count, disperse, pos: Pos, atlas: TextureAtlas, group: SpriteGroup) =
     
     let createSprite () =
         let delta = Pos(GameRandom.random.NextSingle() - 0.5f, GameRandom.random.NextSingle() - 0.5f)
         let texture = GameRandom.choice textures
         let sprite = Sprite(pos, atlas, texture)
         sprite.Height <- scale
         sprite.Width <- scale
         group.AddSprite(sprite)
         let animatablePos: IAnimatablePos = LinearAnimatablePos(1.6f, pos + delta)
         animatablePos.SetTarget(pos + delta * disperse)
         EffectParticle(scale, animatablePos, sprite)
         
     let sprites = Array.init count (fun _ -> createSprite ())
     
     interface Effect with
         member x.Update(dt: float32) =
             Array.iter (fun (x: EffectParticle) -> x.Update(dt)) sprites

         member x.Finished() =
             Array.forall (fun (x: EffectParticle) -> x.Finished()) sprites
             
         member x.Destroy() =
             let sprites = sprites |> Seq.map _.GetSprite()
             Seq.iter (fun x -> group.Sprites.Remove(x) |> ignore) sprites

type DeathEffect(a, b, c) =
    inherit ExplosionEffect([Resources.Particle.SMOKE1; Resources.Particle.SMOKE2], 0.9f, 40, 5f, a, b, c)
    
type TearPopEffect(a, b, c) =
    inherit ExplosionEffect([Resources.Particle.BUBBLE], 0.2f, 1, 2f, a, b, c)