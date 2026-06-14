namespace DMIslandClient.Entity

open DMIslandClient.Resources

module MobGroupTextures =
    let textures = [|
        Resources.Entity.STEVE
        Resources.Entity.LAMBDA
        Resources.Entity.MODUS_PONENS
        Resources.Entity.NERD
        Resources.Entity.NUCLEAR_NERD
        Resources.Entity.MONAD
        Resources.Entity.SKOLEM
        Resources.Entity.MOLE
        Resources.Particle.BUBBLE
        Resources.Particle.ENEMY_PROJECTILE
        Resources.Item.CPP
        Resources.Item.HASKELL
        Resources.Item.PYTHON3
        Resources.UI.HALF_HEART
        Resources.UI.FULL_HEART
        Resources.Item.AMETHYST_SHARD
        Resources.Item.JAVA
        Resources.Item.OCAML
        Resources.Item.GO
        Resources.Item.ZIG
        Resources.Item.RUST
        Resources.Item.ANSIC
        Resources.Item.FSHARP
        Resources.Item.ROC
        Resources.Item.ONEF
        Resources.Item.JS
        Resources.Item.TS
        Resources.Item.KOTLIN
        Resources.Item.X86
        Resources.Item.SCALA3
        Resources.Particle.THETA
        Resources.Particle.ATTACK_INDICATOR
        Resources.Particle.LIGHTNING
        Resources.Entity.GOLDEN_FREDDY
    |]

type MobGroup() =
    inherit EntityGroup(MobGroupTextures.textures, MobFactory())