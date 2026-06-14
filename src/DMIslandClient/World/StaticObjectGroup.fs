namespace DMIslandClient.World

open DMIslandClient.Animation.AnimatablePos
open DMIslandClient.Connection.Dto
open DMIslandClient.Entity
open DMIslandClient.Resources
open DmIslandClient.Utils
open LadaEngine

module WallTextures =
    let textures = [|
        Resources.Texture.STONE_1
        Resources.Texture.STONE_2
        Resources.Texture.GRASS_WALL
        Resources.Texture.THORNS
        Resources.Texture.STONE_DARK
    |]

/// The full set of textures packed into the static-object atlas: every wall
/// variant plus the exit portal. The atlas can only draw textures listed here.
module StaticObjectTextures =
    let all =
        Array.append
            WallTextures.textures
            [| Resources.Entity.EXIT_PORTAL; Resources.Entity.EXIT_PORTAL_CLOSED |]

type StaticObjectFactory(wallTextures: string seq) =
    let mutable textures = wallTextures

    let createWall atlas (group: SpriteGroup) pos =
        let texture = GameRandom.choice textures
        let sprite = Sprite(pos, atlas, texture)
        group.AddSprite(sprite)
        Entity(sprite, LinearAnimatablePos(1f, pos), 1f)

    let createExitWith texture atlas (group: SpriteGroup) pos =
        let sprite = Sprite(pos, atlas, texture)
        group.AddSprite(sprite)
        Entity(sprite, LinearAnimatablePos(1f, pos), 1f)

    interface IEntityFactory with
        member _.CreateEntity(t, atlas, group, pos) =
            match t with
            | EntityType.Exit -> createExitWith Resources.Entity.EXIT_PORTAL atlas group pos
            | EntityType.ExitClosed -> createExitWith Resources.Entity.EXIT_PORTAL_CLOSED atlas group pos
            | EntityType.Wall -> createWall atlas group pos
            | _ -> createWall atlas group pos

    member x.SetTextures(newTextures) =
        textures <- newTextures


type StaticObjectGroup() =
    let factory = StaticObjectFactory(WallTextures.textures)
    let actualGroup = EntityGroup(StaticObjectTextures.all, factory)
    member x.GetGroup() = actualGroup
    member x.SetBiome(biome) =
        match biome with
        | "swamp" -> [| Resources.Texture.STONE_DARK; Resources.Texture.THORNS |] |> factory.SetTextures
        | "nerd" -> [| Resources.Texture.STONE_DARK; Resources.Texture.THORNS |] |> factory.SetTextures
        | "forest" -> [| Resources.Texture.STONE_1; Resources.Texture.STONE_2; Resources.Texture.THORNS |] |> factory.SetTextures
        | _ -> [| Resources.Texture.STONE_1; Resources.Texture.STONE_2 |] |> factory.SetTextures
    