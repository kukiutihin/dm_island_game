namespace DMIslandClient.UI.Components

open DMIslandClient.Resources
open LadaEngine

type HeartType = Empty | Full | Half

type HealthBarHeart(group: SpriteGroup, atlas: TextureAtlas, pos: Pos, size: float32) =
    let container = Sprite(pos, atlas, Resources.UI.HEART_CONTAINER)
    let fullHeart = Sprite(pos, atlas, Resources.UI.FULL_HEART)
    let halfHeart = Sprite(pos, atlas, Resources.UI.HALF_HEART)
    
    do  group.AddSprite(container)
        group.AddSprite(fullHeart)
        group.AddSprite(halfHeart)
    
    member x.SetState(state) =
        container.Width <- size
        container.Height <- size
        fullHeart.Width <- 0f
        fullHeart.Height <- size
        halfHeart.Width <- 0f
        halfHeart.Height <- size
        match state with
        | Empty -> ()
        | Full -> fullHeart.Width <- size
        | Half -> halfHeart.Width <- size


type HealthBar() =
    let mutable origin = Pos(0, 0)
    let mutable size = 0.1f
    let mutable health = 6
    let atlas = TextureAtlas([Resources.UI.HEART_CONTAINER; Resources.UI.HALF_HEART; Resources.UI.FULL_HEART])
    let group = SpriteGroup(atlas)
    let hearts = ResizeArray()
    
    let recreateHearts () =
        group.Sprites.Clear()
        hearts.Clear()
        Seq.init 3 (fun x -> HealthBarHeart(group, atlas, origin + Pos(float32 x * size, 0f), size))
        |> hearts.AddRange
        
    let stateOfLeft left =
        if left >= 2 then Full
        else if left = 1 then Half
        else Empty
    
    member x.UpdateHealth(newHealth) =
        health <- newHealth
        Seq.iteri (fun i (c: HealthBarHeart) -> c.SetState(stateOfLeft (health - i * 2))) hearts
        group.Update()
    
    member x.Render(camera) =
        group.Render(camera)
    
    member x.SetPosition(position) =
        origin <- position
        recreateHearts()
        x.UpdateHealth(health)
        group.Update()
    
    member x.SetScale(scale) =
        size <- scale
        recreateHearts()
        x.UpdateHealth(health)
        group.Update()
