namespace DMIslandClient.Entity

open System
open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Connection.Dto
open DMIslandClient.Resources
open LadaEngine


type IEntityFactory =
    abstract member CreateEntity: EntityType * TextureAtlas * SpriteGroup * Pos -> Entity


type MobFactory() =
    let createLambda atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.LAMBDA)
        let entity = Entity(sprite, EaseOutAnimatablePos(4f, pos))
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity
    
    let createMp atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.MODUS_PONENS)
        let entity = Entity(sprite, SmoothAnimatablePos(4f, pos))
        sprite.Height <- 0.4f
        spriteGroup.AddSprite(sprite)
        entity.SetFlip(false)
        entity
        
    let createProjectile atlas (spriteGroup: SpriteGroup) texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        Entity(sprite, SmoothAnimatablePos(4f, pos))

    let createPlayer atlas (spriteGroup: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, Resources.Entity.STEVE)
        spriteGroup.AddSprite(sprite)
        let entity = Entity(sprite, EaseOutAndBounceAnimatablePos(0.5f, 4f, pos))
        entity.SetFlip(true)
        entity
        
    let createItem atlas (spriteGroup: SpriteGroup) texture pos =
        let sprite = Sprite(pos, atlas, texture)
        spriteGroup.AddSprite(sprite)
        sprite.Height <- 0.6f
        sprite.Width <- 0.6f
        Entity(sprite, SmoothAnimatablePos(4f, pos))
    
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
            | EntityType.Monad -> failwith "todo"
            | EntityType.HeartItem -> createItem atlas group Resources.UI.FULL_HEART pos
            | EntityType.HalfHeartItem -> createItem atlas group Resources.UI.HALF_HEART pos
            | EntityType.AmethystItem -> createItem atlas group Resources.Item.AMETHYST_SHARD pos
