namespace DMIslandClient

module Program =
    let f (a: 'a -> 'a): 'b = a
    
    [<EntryPoint>]
    let main args =
        let game = Game()
        game.Run()
        0