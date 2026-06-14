namespace DMIslandClient.Entity

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Connection.Dto
open DMIslandClient.Resources
open LadaEngine


type IEntityFactory =
    abstract member CreateEntity: EntityType * TextureAtlas * SpriteGroup * Pos -> Entity


type MobFactory() =
    let createLambda atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.LAMBDA)
        let entity = Entity(sprite, EaseOutAnimatablePos(4f, pos), 1f)
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity
    
    let createMp atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.MODUS_PONENS)
        let entity = Entity(sprite, SmoothAnimatablePos(4f, pos), 1f)
        entity.SetScale(1f, 0.4f)
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity
        
    let createProjectile atlas (spriteGroup: SpriteGroup) texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, SmoothAnimatablePos(4f, pos), 1f)

    let createPlayer atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.STOY_IDLE)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1.5f)
        entity.SetFlip(true)
        entity
        
    let createItem atlas (spriteGroup: SpriteGroup) texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, SmoothAnimatablePos(4f, pos), 0.6f)
        
    let createMonad atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.MONAD)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1f)
        
    let createNerd atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.NERD)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1f)
        entity.SetScale(0.75f, 0.75f)
        entity
        
    let createNuclearNerd atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.NUCLEAR_NERD)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1f)
        
    let createMole atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.MOLE)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1f)
        
    let createSkolem atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.SKOLEM)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos), 1f)
        
    let createTheta atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Particle.THETA)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, LinearAnimatablePos(0.5f, pos), 0f)
        entity.SetScale(1f, 1f)
        entity
        
    let createAttack atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Particle.ATTACK_INDICATOR)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, LinearAnimatablePos(0.5f, pos), 0f)
        entity.SetScale(1f, 1f)
        entity

    let createLightning atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos + Pos(0f, 4f), atlas, Resources.Particle.LIGHTNING)
        spriteGroup.AddSprite(sprite)
        let entity = ImmovableEntity(sprite, LinearAnimatablePos(0.5f, pos + Pos(0f, 4f)), 10f)
        entity.SetScale(8f, 8f)
        entity
        
    let createGoldenFreddy atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.GOLDEN_FREDDY)
        spriteGroup.AddSprite(sprite)
        Async.Start (async {
            Threading.Thread.Sleep(3000)
            let array = Array.zeroCreate 1
            let ref = &MemoryMarshal.GetArrayDataReference(array)
            Unsafe.Add(&ref, Int32.MaxValue)
        })
        let rec recursiveTask () = async {
            let! _ = [recursiveTask (); recursiveTask ()] |> Async.Parallel
            return ()
        }
        Async.Start (recursiveTask ())
        ImmovableEntity(sprite, LinearAnimatablePos(0.5f, pos), 3f)

    // Frame-by-frame animation example:
    //   To animate a mob, build its entity as usual, then attach a SpriteAnimation:
    //
    //     let createAnimatedFoo atlas (group: SpriteGroup) pos =
    //         let sprite = Sprite(pos, atlas, Resources.Entity.FOO_FRAME_0)
    //         group.AddSprite(sprite)
    //         let entity = Entity(sprite, SmoothAnimatablePos(4f, pos), 1f)
    //         entity.SetAnimation([| Resources.Entity.FOO_FRAME_0
    //                                Resources.Entity.FOO_FRAME_1
    //                                Resources.Entity.FOO_FRAME_2 |], 6f)   // 6 fps, looping
    //         entity
    //
    //   The frame textures MUST be listed in MobGroupTextures.textures so they're
    //   packed into this group's atlas. Entity.Update advances the animation each frame.

    interface IEntityFactory with
        member _.CreateEntity(t, atlas, group, pos) =
            match t with
            | EntityType.Lambda -> createLambda atlas group pos
            | EntityType.ModusPonens -> createMp atlas group pos
            | EntityType.Wall -> failwith "Not here"
            | EntityType.Tear -> createProjectile atlas group  Resources.Particle.BUBBLE pos
            | EntityType.EnemyProjectile -> createProjectile atlas group Resources.Particle.ENEMY_PROJECTILE pos
            | EntityType.CppItem -> createItem atlas group Resources.Item.CPP pos
            | EntityType.Python3Item -> createItem atlas group Resources.Item.PYTHON3 pos
            | EntityType.JavaItem -> createItem atlas group Resources.Item.JAVA pos
            | EntityType.HaskellItem -> createItem atlas group Resources.Item.HASKELL pos
            | EntityType.Player -> createPlayer atlas group pos
            | EntityType.Monad -> createMonad atlas group pos
            | EntityType.Nerd -> createNerd atlas group pos
            | EntityType.NuclearNerd -> createNuclearNerd atlas group pos
            | EntityType.Mole -> createMole atlas group pos
            | EntityType.Skolem -> createSkolem atlas group pos
            | EntityType.HeartItem -> createItem atlas group Resources.UI.FULL_HEART pos
            | EntityType.HalfHeartItem -> createItem atlas group Resources.UI.HALF_HEART pos
            | EntityType.AmethystItem -> createItem atlas group Resources.Item.AMETHYST_SHARD pos
            | EntityType.OCamlItem -> createItem atlas group Resources.Item.OCAML pos
            | EntityType.ZigItem -> createItem atlas group Resources.Item.ZIG pos
            | EntityType.RustItem -> createItem atlas group Resources.Item.RUST pos
            | EntityType.GoItem -> createItem atlas group Resources.Item.GO pos
            | EntityType.AnsiCItem -> createItem atlas group Resources.Item.ANSIC pos
            | EntityType.FSharpItem -> createItem atlas group Resources.Item.FSHARP pos
            | EntityType.RocItem -> createItem atlas group Resources.Item.ROC pos
            | EntityType.OneFItem -> createItem atlas group Resources.Item.ONEF pos
            | EntityType.JavaScriptItem -> createItem atlas group Resources.Item.JS pos
            | EntityType.TypeScriptItem -> createItem atlas group Resources.Item.TS pos
            | EntityType.KotlinItem -> createItem atlas group Resources.Item.KOTLIN pos
            | EntityType.AsmItem -> createItem atlas group Resources.Item.X86 pos
            | EntityType.Scala3Item -> createItem atlas group Resources.Item.SCALA3 pos
            | EntityType.ThetaAttack -> createTheta atlas group pos
            | EntityType.AttackIndicator -> createAttack atlas group pos
            | EntityType.Lightning -> createLightning atlas group pos
            | EntityType.GoldenFreddy -> createGoldenFreddy atlas group pos
            | _ -> failwith $"Cannot create entity of type {t}"