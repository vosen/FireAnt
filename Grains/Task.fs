namespace FireAnt.Grains

// Mostly taken from FSharpx with small fixes and additions
module Task =
    open System
    open System.Threading
    open System.Threading.Tasks

    let Finished = Task.FromResult(())
    let Done = Orleans.TaskDone.Done
    let InlineContinuation = TaskContinuationOptions.ExecuteSynchronously ||| TaskContinuationOptions.DenyChildAttach

    let FromCanceled<'T>() : Task<'T> =
        let tcs = TaskCompletionSource()
        tcs.SetCanceled()
        tcs.Task

    type TaskBuilder(?scheduler, ?cancellationToken) =
        let scheduler = defaultArg scheduler TaskScheduler.Default
        let cancellationToken = defaultArg cancellationToken CancellationToken.None

        member this.Return x = Task.FromResult(x)

        member this.Zero() = Finished

        member this.ReturnFrom (a: Task) = a
        
        member this.ReturnFrom (a: Task<'T>) = a
        
        member this.Bind(m: Task, f: unit -> Task<'U>) : Task<'U> =
            m.ContinueWith((fun (t: Task) (f: obj) -> (f :?> unit -> Task<'U>)()), f :> obj, cancellationToken, InlineContinuation, scheduler).Unwrap()

        member this.Bind(m: Task<'T>, f: 'T -> Task<'U>) : Task<'U> =
            m.ContinueWith((fun (t: Task<'T>) (f: obj) -> (f :?> 'T -> Task<'U>)(t.Result)), f :> obj, cancellationToken, InlineContinuation, scheduler).Unwrap()

        member this.Combine(comp1, comp2) =
            this.Bind(comp1, comp2)

        member this.While(guard, m) =
            if not(guard()) then this.Zero() else
                this.Bind(m(), fun () -> this.While(guard, m))

        member this.TryWith(body:unit -> Task<_>, catchFn:exn -> Task<_>) =  
            try
               body()
                .ContinueWith((fun (t:Task<_>) ->
                   match t.IsFaulted with
                   | false -> t
                   | true  -> catchFn(t.Exception.GetBaseException())),
                   InlineContinuation)
                .Unwrap()
            with e -> catchFn(e)

        member this.TryFinally(m, compensation) =
            try this.ReturnFrom m
            finally compensation()

        member this.Using(res: #IDisposable, body: #IDisposable -> Task<_>) =
            this.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

        member this.For(sequence: seq<_>, body) =
            this.Using(sequence.GetEnumerator(),
                                 fun enum -> this.While(enum.MoveNext, fun () -> body enum.Current))

        member this.Delay (f: unit -> Task<'T>) = f

        member this.Run (f: unit -> Task<'T>) = f()