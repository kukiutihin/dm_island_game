namespace DMIslandClient.Connection

open System
open System.Net.Http.Json
open System.Text.Json
open System.Text.Json.Serialization
open DMIslandClient.Connection.Dto
open System.Net.Http
type GameConnection(serverUrl) =
    let actionEndpoint = $"{serverUrl}/action"
    
    let options = JsonSerializerOptions()
    do options.Converters.Add(JsonStringEnumConverter())
    
    let sendRequest (req: PlayerActionRequest) = task {
        use client = new HttpClient()
        let content = JsonContent.Create(req)
        let! response = client.PostAsync(actionEndpoint, content) |> Async.AwaitTask
        return response
    }
    
    let actionAndCallback (action: PlayerActionRequest) (callback: GameStateResponse -> unit) = task {
        let! response = sendRequest action |> Async.AwaitTask
        let! content = response.Content.ReadFromJsonAsync<GameStateResponse>(options) |> Async.AwaitTask
        callback content
    }
    
    member _.SendCallback(req: PlayerActionRequest, callback: GameStateResponse -> unit) = task {
        let! response = sendRequest req |> Async.AwaitTask
        let! content = response.Content.ReadFromJsonAsync<GameStateResponse>(options) |> Async.AwaitTask
        callback content
    }
    
    member _.MoveCallback(direction: string, callback: GameStateResponse -> unit) =
        let action = { Action = "move"; Direction = Some direction }
        actionAndCallback action callback |> Async.AwaitTask |> Async.RunSynchronously
    
    member _.ShootCallback(direction: string, callback: GameStateResponse -> unit) =
        let action = { Action = "attack"; Direction = Some direction }
        actionAndCallback action callback |> Async.AwaitTask |> Async.RunSynchronously
    
    member _.SkibCallback(callback: GameStateResponse -> unit) =
        let action = { Action = "skip"; Direction = None  }
        actionAndCallback action callback |> Async.AwaitTask |> Async.RunSynchronously

    member _.RestartCallback(callback: GameStateResponse -> unit) =
        let action = { Action = "restart"; Direction = None }
        actionAndCallback action callback |> Async.AwaitTask |> Async.RunSynchronously

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