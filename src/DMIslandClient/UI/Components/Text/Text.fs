namespace DMIslandClient.UI.Text

open System.Collections.Generic
open DMIslandClient.Resources
open LadaEngine

type CharDescription = {
    texture: string
    ratio: float32
}

type Glyph(atlas: TextureAtlas, charData: CharDescription, pos: Pos, spriteGroup: SpriteGroup, scale: float32) =
    let sprite = Sprite(pos + Pos(charData.ratio / 2f * scale, 0f), atlas, charData.texture)
    let () =
        sprite.Width <- charData.ratio * scale
        sprite.Height <- scale
        spriteGroup.AddSprite(sprite)
    member x.GetCharDescription() = charData

type Font(fontPrefix: string) =
    let charDescriptions = Dictionary<char, CharDescription>()
    let usedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890-+=<>.,:;/'\"\\!@#$%^&*()[]{} "
    
    let charToTexture x =
        $"{fontPrefix}{int x}.png"

    let buildAtlas () =
        let usedFiles = Seq.map charToTexture usedChars
        TextureAtlas(usedFiles)
    
    let createCharDescription char =
        let path = charToTexture char
        let image = SixLabors.ImageSharp.Image.Load(path)
        let ratio = float32 image.Width / float32 image.Height
        { texture = path; ratio = ratio }
    
    let populateDescriptions () =
        Seq.iter (fun c -> charDescriptions.Add(c, createCharDescription c)) usedChars
    
    let atlas = buildAtlas ()
    let () = populateDescriptions ()
    
    member x.GetAtlas() = atlas
    
    member x.GetCharData(c: char) =
        charDescriptions[c]
    

type Text(content: string, origin: Pos) =
    let mutable position = origin
    let mutable scale = 1f
    let mutable text = content
    let mutable glyphs = ResizeArray()
    let font = Font(Resources.Font.FONT_QUICKSAND_PREFIX)
        
    let atlas = font.GetAtlas()
    let group = SpriteGroup(atlas)
    
    let genGlyph (char: char) pos =
        let charData = font.GetCharData(char)
        Glyph(atlas, charData, pos, group, scale)
    
    let setText text =
        group.Sprites.Clear()
        let mutable pos = position
        let appendGlyph c =
            let glyph = genGlyph c pos
            let description = glyph.GetCharDescription()
            pos <- pos + Pos(description.ratio * scale, 0f)
            description.texture
        glyphs <- Seq.map appendGlyph text |> ResizeArray
    
    let () = setText text
    
    member x.SetText(text: string) =
        setText text
    
    member x.Update(dt) =
        group.Update()
        
    member x.Render(camera: Camera) =
        group.Render(camera)
    
    member x.SetScale(size: float32) =
        scale <- size
        setText text
    
    member x.SetPosition(origin: Pos) =
        position <- origin
        setText text
