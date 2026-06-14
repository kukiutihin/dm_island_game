namespace DMIslandClient

open Argu

module Program =
    [<EntryPoint>]
    let main args =
        let parser = ArgumentParser.Create<CliArguments>()
        let results = parser.Parse(args)
        let all = results.GetAllResults()
        let game = Game(all)
        game.Run()
        0