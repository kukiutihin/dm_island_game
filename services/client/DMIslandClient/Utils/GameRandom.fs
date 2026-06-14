namespace DmIslandClient.Utils

open System

module GameRandom =
    let random = Random()
    
    let choice (collection : 'a seq) =
        let length = Seq.length collection
        let index = random.Next(length)
        Seq.item index collection

