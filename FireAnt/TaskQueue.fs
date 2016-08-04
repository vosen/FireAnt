namespace FireAnt

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Akka.Actor

// I'll convert it to something better one day
type TaskQueue(owner: ICanTell) =
    let lockObj = obj()
    let queue = Queue()
    let waitHandle = new ManualResetEventSlim()
    let thread = Thread(fun () ->
        while (waitHandle.Wait(); true) do
            let func = lock lockObj (fun () -> 
                let value = queue.Dequeue()
                if queue.Count = 0 then
                    waitHandle.Reset()
                value
            )
            try
                func()
            with
                | exn -> owner.Tell(Status.Failure(exn), ActorRefs.NoSender)
    )
    do
        thread.Start()

    member t.Enqueue(f: unit -> unit) =
        lock lockObj (fun() ->
            queue.Enqueue(f)
            if queue.Count = 1 then
                waitHandle.Set()
        )