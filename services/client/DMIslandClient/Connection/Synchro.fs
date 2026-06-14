namespace DMIslandClient.Connection

open System.Collections.Concurrent

type SynchroQueue() =
    let queue = ConcurrentQueue()

    member _.AddEvent(x: unit -> unit) =
        queue.Enqueue(x)

    member this.ExecuteAll() =
        match queue.TryDequeue() with true, x -> x (); this.ExecuteAll() | _ -> ()
