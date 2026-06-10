namespace DMIslandClient.Entity

open DMIslandClient.Resources

module MobGroupTextures =
    let textures = [|
        Resources.Entity.STEVE
        Resources.Entity.LAMBDA
        Resources.Entity.MODUS_PONENS
        Resources.Particle.BUBBLE
        Resources.Particle.ENEMY_PROJECTILE
        Resources.Item.CPP
        Resources.Item.HASKELL
        Resources.Item.PYTHON3
        Resources.UI.HALF_HEART
        Resources.UI.FULL_HEART
        Resources.Item.AMETHYST_SHARD
        Resources.Item.JAVA 
    |]

type MobGroup() =
    inherit EntityGroup(MobGroupTextures.textures, MobFactory())