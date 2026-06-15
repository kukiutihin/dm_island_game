namespace DMIslandClient.Connection

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open System.Text.Json.Serialization
open DMIslandClient.Connection.Dto

type PingGameConnection(serverUrl) =
    let actionEndpoint = $"{serverUrl}/action"
    let mutable connectionSingleton = None
    
    let options = JsonSerializerOptions()
    do options.Converters.Add(JsonStringEnumConverter())
    
    let sendRequest (req: PlayerActionRequest) = task {
        use client = new HttpClient()
        let content = JsonContent.Create(req)
        let! response = client.PostAsync(actionEndpoint, content) |> Async.AwaitTask
        return response
    }
    
    let actionAndCallback (action: PlayerActionRequest) (callback: GameStateResponse -> unit) = task {
        Threading.Thread.Sleep(10)
        let! response = sendRequest action |> Async.AwaitTask
        let! content = response.Content.ReadFromJsonAsync<GameStateResponse>(options) |> Async.AwaitTask
        callback content
    }
    
    interface IGameConnection with
        member _.SendCallback(req: PlayerActionRequest, callback: GameStateResponse -> unit) =
            actionAndCallback req callback
        
        member _.MoveCallback(_: string, _: GameStateResponse -> unit) = ()
        member _.ShootCallback(_: string, _: GameStateResponse -> unit) = ()
        member _.SkibCallback(callback: GameStateResponse -> unit) = 
            if connectionSingleton.IsSome then () else
            connectionSingleton <- Some callback 
            let rec loop () = task {
                actionAndCallback { Action = "ping"; Direction = None } callback
                |> Async.AwaitTask
                |> Async.RunSynchronously
                do! Async.Sleep(100)
                do! loop ()
            }
            loop () |> Async.AwaitTask |> Async.Start

        member _.RestartCallback(_: GameStateResponse -> unit) = ()
        member _.CheckAlive(callback: bool -> unit) =
            let action = { Action = "ping"; Direction = None }
            try
                actionAndCallback action ignore |> Async.AwaitTask |> Async.RunSynchronously
                callback true
            with
            | :? HttpRequestException -> callback false
            | :? AggregateException as e ->
                printfn "%s" <| e.Message.ToString()
                printfn "%s" <| e.StackTrace.ToString()
                callback false
