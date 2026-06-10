namespace LadaEngine

open System

/// A textured quad. Add it to a SpriteGroup to render it.
type Sprite(position: Pos, atlas: ITextureAtlas, textureName: string) =
    member val Position = position with get, set
    member val Width = 1f with get, set
    member val Height = 1f with get, set
    member val Rotation = 0f with get, set
    /// Z level of the sprite
    member val Level = 1 with get, set
    member val Group = -1 with get, set
    member val TextureName = textureName with get, set

    member _.Atlas = atlas

    /// Identical copy of this sprite
    member this.Copy() =
        Sprite(this.Position, atlas, this.TextureName, Width = this.Width, Height = this.Height)

    /// Appends this sprite's vertex data (4 corners x [x, y, z, u, v]) to the buffer
    member internal this.AppendVerts(buffer: ResizeArray<float32>) =
        let coords = atlas.GetCoords this.TextureName
        let z = 1f / (1.1f + float32 this.Level)
        let radius = sqrt (this.Width * this.Width + this.Height * this.Height) / 2f

        // Angles from the center to each corner of the unrotated quad
        let cornerA = atan2 this.Height this.Width
        let cornerB = atan2 this.Height (-this.Width)
        let angle = MathF.PI - this.Rotation
        let p = this.Position

        let append relAngle (u: float32) (v: float32) =
            buffer.Add(p.X + radius * cos (angle + relAngle))
            buffer.Add(p.Y + radius * sin (angle + relAngle))
            buffer.Add z
            buffer.Add u
            buffer.Add v

        append (cornerA + MathF.PI) coords[0] coords[1] // top right
        append cornerB coords[2] coords[3] // bottom right
        append cornerA coords[4] coords[5] // bottom left
        append (cornerB + MathF.PI) coords[6] coords[7] // top left
