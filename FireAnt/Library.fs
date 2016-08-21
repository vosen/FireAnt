namespace FireAnt
#nowarn "686" // suppress warnings about createFrom

open Akka.Actor
open Akka.Routing

open System
open System.IO
open System.Collections.Generic
open FireAnt.Transport

// atm its implicitly a stack, but with little bit of effort this could be turned into a queue
type private HashBag<'a when 'a : equality>() =
    let set = HashSet()
    let list = ResizeArray()

    member t.Count = list.Count

    member t.Add a =
        if set.Add(a) then
            list.Add(a)

    member t.Take() =
        let elm = list.[list.Count - 1]
        let removed = set.Remove(elm)
        System.Diagnostics.Debug.Assert(removed)
        list.RemoveAt(list.Count - 1)
        elm

// This should be made so the newly inserted item is at the end of the queue, atm it's effectively random
type private MultiRoundRobin<'a when 'a : equality>() =
    let dict = Dictionary()
    let list = ResizeArray()
    [<DefaultValue>] val mutable index: int

    member t.IsEmpty = list.Count = 0

    member t.Push(a: 'a, count: int) : unit =
        if count > 0 then
            match dict.TryGetValue(a) with
            | true, value -> value := !value + count
            | _, _ ->
                let count = ref count
                dict.Add(a, count)
                list.Add((a, count))

    member t.Pop() : 'a =
        let current = (t.index % list.Count)
        let (item, count) = list.[current]
        decr count
        if !count = 0 then
            dict.Remove(item) |> ignore
            list.RemoveAt(current)
        else
            t.index <- (t.index + 1) % list.Count
        item

module Message =
    module Worker =
        type Discover = { RunId: string; }
        type Run = { Id: string; Tests: IReadOnlyList<TestCase> }

    module WorkerSet =
        type CoordinatedBy() = class end

    module Coordinator =
        type Start = { Id: string; }
        type Ready = { Workers: IActorRef[] }
        type GetWorkers = { Count: int }

    module Dispatcher =
        type Start = { Client: IActorRef; Id: string }
        type Discovered = { Id: string; Tests: IReadOnlyList<IReadOnlyList<TestCase>> }
        type PartialResult = { Id: string; Result: TestResultSummary }
        type FinishedBatch() = class end
        type Continue = { Worker: IActorRef }

    module Client =
        type Started() = class end
        type PartialResult = { Result: TestResultSummary }
        type Finished() = class end

module Worker =
    open FireAnt.Akka.FSharp
    open FSharp.Control

    module Message = Message.Worker
    module Dispatcher = Message.Dispatcher

    type Actor private(builder: IWorkspaceBuilder, timing: ITestTimeRepository, splitter: ISplitStrategy) as t =
        inherit AsyncActor()
        do
            base.ReceiveRespond(t.OnReceiveRun)
            base.ReceiveRespond(t.OnReceiveDiscover)
        static member Create(b, t, s) = Actor(b, t, s)
        static member Configure (props: Props) = props
        static member Path (id: int) : string = string id

        member private t.OnReceiveDiscover (msg: Message.Discover, callback: Dispatcher.Discovered -> unit) =
            let path = Actor.Context.Self.Path
            async {
                let! tests = Test.Discover(path, (fun () -> builder.Build msg.RunId))
                let splitTests = splitter.Split(tests |> Seq.map(fun t -> (t, timing.GetPredicted(t))) |> ResizeArray)
                callback({ Dispatcher.Discovered.Id = msg.RunId; Dispatcher.Discovered.Tests = splitTests })
            } |> Async.Start

        member private t.OnReceiveRun (msg: Message.Run, callback: obj -> unit) : unit =
            let path = Actor.Context.Self.Path
            async{
                for result in Test.Run(path, (fun () -> builder.Build msg.Id), msg.Tests) do
                    callback({ Dispatcher.PartialResult.Id = msg.Id; Dispatcher.PartialResult.Result = result })
                callback(Dispatcher.FinishedBatch())
            } |> Async.Start

module WorkerSet =
    open FireAnt.Akka.FSharp
    module Message = Message.WorkerSet
    open Message

    type Actor private(coordinator: IActorRef, builder: IWorkspaceBuilder, timing: ITestTimeRepository, splitter: ISplitStrategy) as t =
        inherit ReceiveActor()
        [<DefaultValue>] val mutable children : IActorRef[]
        do
            base.Receive<Message.CoordinatedBy>(Action<Message.CoordinatedBy>(t.OnReceive))
        static member Create(c,b,t,s) = Actor(c,b,t,s)
        static member Configure (props: Props) = props
        static member Path = "runner"
        override t.PreStart() =
            let context = Actor.Context
            t.children <- Array.init (Environment.ProcessorCount) (fun i -> context.ActorOf(Props.create3<Worker.Actor, _, _, _>(builder, timing, splitter), Worker.Actor.Path (i + 1)))
            coordinator.Tell({ Message.Coordinator.Ready.Workers = t.children })
        member private t.OnReceive(msg: CoordinatedBy) =
            let context = Actor.Context
            context.Sender.Tell({ Message.Coordinator.Ready.Workers = t.children })

module Dispatcher =
    module Message = Message.Dispatcher
    module Client = Message.Client
    module Coordinator = Message.Coordinator
    type DiscoverMessage = Message.Worker.Discover
    type RunMessage = Message.Worker.Run

    type Run = { Client: IActorRef; Id: string }
    type State =
        | Initialized
        | WaitForDiscoveryWorker of run: Run
        | WaitForDiscoveryResult of run: Run
        | RunTests of run: Run * running: int * waiting: Queue<IReadOnlyList<TestCase>>

        member t.Client : IActorRef =
            match t with
            | Initialized -> invalidOp null
            | WaitForDiscoveryWorker({ Client = client; }) -> client
            | WaitForDiscoveryResult({ Client = client; }) -> client
            | RunTests({ Client = client; }, _, _) -> client

        member t.IsFinished =
            match t with
            | RunTests(_, 0, waiting) when waiting.Count = 0 -> true
            | _ -> false

        member t.Start(ctx: IActorContext, client: IActorRef, id: string) =
            match t with
            | Initialized ->
                ctx.Parent.Tell({ Coordinator.GetWorkers.Count = 1 })
                client.Tell(Message.Client.Started())
                State.WaitForDiscoveryWorker({ Client = client; Id = id })
            | _ -> invalidOp null

        member t.ReceiveLocal(ctx: IActorContext, worker: IActorRef) =
            match t with
            | WaitForDiscoveryWorker run ->
                worker.Tell({ DiscoverMessage.RunId = run.Id })
                State.WaitForDiscoveryResult(run)
            | RunTests(run, running, waiting) ->
                if waiting.Count > 0 then
                    worker.Tell({ RunMessage.Id = run.Id; RunMessage.Tests = waiting.Dequeue() })
                    State.RunTests(run, running + 1, waiting)
                else
                    ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| worker |] })
                    invalidOp null
            | _ ->
                ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| worker |] })
                invalidOp null

        member t.ReceiveRemote(ctx: IActorContext, runId: string, tests: IReadOnlyList<IReadOnlyList<TestCase>>) =
            let state = match t with
                        | WaitForDiscoveryResult run when run.Id = runId ->
                            ctx.Parent.Tell({ Message.Coordinator.GetWorkers.Count = tests.Count })
                            State.RunTests(run = run, running = 0, waiting = Queue(tests))
                        | _ -> t
            ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| ctx.Sender |] })
            state

        member t.ReceiveBatchFinish(ctx: IActorContext) =
            let state = match t with
                        | RunTests(run, running, waiting) ->
                            State.RunTests(run, running - 1, waiting)
                        | _ -> t
            ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| ctx.Sender |] })
            state

    type Actor private(timing: ITestTimeRepository) as t =
        inherit ReceiveActor()
        [<DefaultValue>] val mutable state: State
        do
            base.Receive<Message.Start>(Action<Message.Start>(t.OnReceive))
            base.Receive<Message.Continue>(Action<Message.Continue>(t.OnReceive))
            base.Receive<Message.Discovered>(Action<Message.Discovered>(t.OnReceive))
            base.Receive<Message.PartialResult>(Action<Message.PartialResult>(t.OnReceive))
            base.Receive<Message.FinishedBatch>(Action<Message.FinishedBatch>(t.OnReceive))
        static member Create(t) = Actor(t)
        static member Configure (props: Props) = props
        static member Path = "dispatcher"
        override t.PreStart() =
            t.state <- State.Initialized
        member private t.OnReceive(msg: Message.Start) =
            t.state <- t.state.Start(Actor.Context, msg.Client, msg.Id)
        member private t.OnReceive(msg: Message.Continue) =
            t.state <- t.state.ReceiveLocal(Actor.Context, msg.Worker)
        member private t.OnReceive(msg: Message.Discovered) = 
            t.state <- t.state.ReceiveRemote(Actor.Context, msg.Id, msg.Tests)
        member private t.OnReceive(msg: Message.PartialResult) =
            t.state.Client.Tell({ Client.PartialResult.Result = msg.Result })
        member private t.OnReceive(msg: Message.FinishedBatch) =
            let context = Actor.Context
            t.state <- t.state.ReceiveBatchFinish(Actor.Context)
            if t.state.IsFinished then
                t.state.Client.Tell(Client.Finished())
                context.Stop(t.Self)

module Coordinator =
    open FireAnt.Akka.FSharp
    module Message = Message.Coordinator
    open Message

    type Actor private(router: IActorRef, timing: ITestTimeRepository) as t =
        inherit ReceiveActor()
        let readyWorkers = HashBag<IActorRef>()
        let readyDispatchers = MultiRoundRobin<IActorRef>()
        do
            base.Receive<Message.Ready>(Action<Message.Ready>(t.OnReceive))
            base.Receive<Message.Start>(Action<Message.Start>(t.OnReceive))
            base.Receive<Message.GetWorkers>(Action<Message.GetWorkers>(t.OnReceive))
        static member Create(r, t) = Actor(r,t)
        static member Configure (props: Props) = props
        static member Path = "coordinator"
        override t.PreStart() =
            router.Tell(Message.WorkerSet.CoordinatedBy())
        member private t.OnReceive(msg: Message.Ready) =
            for worker in msg.Workers do
                if not readyDispatchers.IsEmpty then
                    let dispatcher = readyDispatchers.Pop()
                    dispatcher.Tell({ Message.Dispatcher.Continue.Worker = worker })
                else
                    readyWorkers.Add(worker) |> ignore
        member private t.OnReceive(msg: Message.Start) =
            let context = Actor.Context
            let dispatcher = t.ActorOf<Dispatcher.Actor, _>(timing)
            dispatcher.Tell({ Message.Dispatcher.Start.Client = context.Sender; Message.Dispatcher.Start.Id = msg.Id })
        member private t.OnReceive(msg: Message.GetWorkers) =
            let sender = Actor.Context.Sender
            let available = min readyWorkers.Count msg.Count
            for _ in 1..available do
                sender.Tell({ Message.Dispatcher.Continue.Worker = readyWorkers.Take() })
            readyDispatchers.Push(sender, msg.Count - available)