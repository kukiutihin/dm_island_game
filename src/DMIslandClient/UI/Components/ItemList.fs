namespace DMIslandClient.UI.Components

open DMIslandClient.Connection.Dto
open DMIslandClient.Resources
open LadaEngine

type ItemList(pos: Pos) =
    let mutable origin = pos
    let mutable scale = 0.1f


    let textures =
        [| Resources.Item.CPP
           Resources.Item.OCAML
           Resources.Item.HASKELL
           Resources.Item.PYTHON3
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
           Resources.Item.SCALA3 |]

    let atlas = TextureAtlas(textures)
    let group = SpriteGroup(atlas)

    let addItem pos typ =
        let texture =
            match typ with
            | ItemType.Cpp -> Resources.Item.CPP
            | ItemType.OCaml -> Resources.Item.OCAML
            | ItemType.Haskell -> Resources.Item.HASKELL
            | ItemType.Java -> Resources.Item.JAVA
            | ItemType.Python3 -> Resources.Item.PYTHON3
            | ItemType.Amethyst -> Resources.Item.AMETHYST_SHARD
            | ItemType.Zig -> Resources.Item.ZIG
            | ItemType.Rust -> Resources.Item.RUST
            | ItemType.AnsiC -> Resources.Item.ANSIC
            | ItemType.FSharp -> Resources.Item.FSHARP
            | ItemType.Roc -> Resources.Item.ROC
            | ItemType.Go -> Resources.Item.GO
            | ItemType.OneF -> Resources.Item.ONEF
            | ItemType.JavaScript -> Resources.Item.JS
            | ItemType.TypeScript -> Resources.Item.TS
            | ItemType.Kotlin -> Resources.Item.KOTLIN
            | ItemType.Asm -> Resources.Item.X86
            | ItemType.Scala3 -> Resources.Item.SCALA3
            | _ -> failwith $"Wrong item to display {typ.ToString()}"
        let sprite = Sprite(pos, atlas, texture)
        sprite.Width <- scale
        sprite.Height <- scale
        group.Sprites.Add(sprite)

    let rebuildItems items =
        group.Sprites.Clear()
        Seq.iteri (fun i t -> addItem (origin + Pos(0f, float32 i * scale)) t) items
        group.Update()

    member x.SetItems(items: ItemType seq) = rebuildItems items

    member x.Render(camera) = group.Render(camera)

    member x.SetOrigin(newOrigin: Pos) = origin <- newOrigin

    member x.SetScale(newScale: float32) = scale <- newScale
