namespace FireAnt
#nowarn "686" // suppress warnings about createFrom

open Akka.Actor
open Akka.Routing

open Xunit
open Xunit.Abstractions
open Xunit.Sdk

open System
open System.IO
open System.Collections.Generic

type IWorkspaceBuilder =
    abstract member Build: string -> FileInfo

type ITestTimeRepository =
    abstract member GetPredicted: string -> decimal
    abstract member Store: runId: string * test: string * time: decimal -> unit

// atm its implicitly a stack, but with little bit of effort this could be turned into a queue
type private HashBag<'a when 'a : equality>() =
    let set = HashSet()
    let list = ResizeArray()

    member t.Count = list.Count

    member t.Add a =
        if set.Add(a) then
            list.Add(a)

    member t.Remove a =
        if set.Remove(a) then
            list.Remove(a)
        else
            false

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

module Transport =
    type TestResult = 
    | Passed
    | Failed
    | Skipped of reason: string

    type TestCaseSummary =
     {
        Name: string
        Result: TestResult
        Time: decimal
        Output: string
     }
     with
        static member Create (output: ITestResultMessage) (result: TestResult) : TestCaseSummary =
            {
                Name = output.TestCase.DisplayName
                Result = result
                Time = output.ExecutionTime
                Output = output.Output
            }

    type RemoteRunResult =
        {
            RunSummary: RunSummary
            TestSummaries: IReadOnlyList<TestCaseSummary>
        }

    module Surrogate =
        let inline createFrom x = if isNull x then null else (^T : (new : ^U -> ^T) x)

        [<AllowNullLiteral>]
        type SourceInformation(src: ISourceInformation) =
            let mutable fileName = src.FileName
            let mutable lineNumber = src.LineNumber
            interface ISourceInformation with
                member t.FileName
                    with get () = fileName
                    and set value = fileName <- value
                member t.LineNumber
                    with get () = lineNumber
                    and set value = lineNumber <- value
                member t.Serialize(_) = invalidOp null
                member t.Deserialize(_) = invalidOp null

        [<AllowNullLiteral>]
        type AssemblyInfo(assembly: IAssemblyInfo) =
            let assemblyPath = assembly.AssemblyPath
            let name = assembly.Name
            interface IAssemblyInfo with
                member t.AssemblyPath = assemblyPath
                member t.Name = name
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetType(_) = invalidOp null
                member t.GetTypes(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TypeInfo(typ: ITypeInfo) =
            let assembly = createFrom<_, AssemblyInfo> typ.Assembly
            let baseType = null // This is slightly wrong, but otherwise we'de have to serialize all the inheritance path
            let interfaces = typ.Interfaces
                             |> Seq.map (fun i -> createFrom<_, TypeInfo> i :> ITypeInfo)
                             |> ResizeArray
            let isAbstract = typ.IsAbstract;
            let isGenericParameter = typ.IsGenericParameter
            let isGenericType = typ.IsGenericType
            let isSealed = typ.IsSealed
            let isValueType = typ.IsValueType
            let name = typ.Name
            interface ITypeInfo with                
                member t.Assembly = assembly :> IAssemblyInfo
                member t.BaseType = baseType
                member t.Interfaces = interfaces :> ITypeInfo seq
                member t.IsAbstract = isAbstract
                member t.IsGenericParameter = isGenericParameter
                member t.IsGenericType = isGenericType
                member t.IsSealed = isSealed
                member t.IsValueType = isValueType
                member t.Name = name
                member t.GetMethod(_, _) = invalidOp null
                member t.GetMethods(_) = invalidOp null
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetGenericArguments() = invalidOp null

        [<AllowNullLiteral>]
        type MethodInfo(minfo: IMethodInfo) =
            let isAbstract = minfo.IsAbstract;
            let isGenericMethodDefinition = minfo.IsGenericMethodDefinition;
            let isPublic = minfo.IsPublic;
            let isStatic = minfo.IsStatic;
            let name = minfo.Name;
            let returnType = createFrom<_, TypeInfo> minfo.ReturnType
            let typ = createFrom<_, TypeInfo> minfo.Type
            interface IMethodInfo with
                member t.IsAbstract = isAbstract
                member t.IsGenericMethodDefinition = isGenericMethodDefinition
                member t.IsPublic = isPublic
                member t.IsStatic = isStatic
                member t.Name = name
                member t.ReturnType = returnType :> ITypeInfo
                member t.Type = typ :> ITypeInfo
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetGenericArguments() = invalidOp null
                member t.GetParameters() = invalidOp null
                member t.MakeGenericMethod(_) = invalidOp null

        [<AllowNullLiteral>]
        type TestAssembly(src: ITestAssembly) =
            let assembly = createFrom<_, AssemblyInfo> src.Assembly
            let configFileName = src.ConfigFileName
            interface ITestAssembly with
                member t.Assembly = assembly :> IAssemblyInfo
                member t.ConfigFileName = configFileName
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestCollection(src: ITestCollection) =
            let collectionDefinition = createFrom<_, TypeInfo> src.CollectionDefinition
            let displayName = src.DisplayName
            let testAssembly = createFrom<_, TestAssembly> src.TestAssembly
            interface ITestCollection with
                member t.CollectionDefinition = collectionDefinition :> ITypeInfo
                member t.DisplayName = displayName
                member t.TestAssembly = testAssembly :> ITestAssembly
                member t.UniqueID =  invalidOp null
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestClass(src: ITestClass) =
            let klass = createFrom<_, TypeInfo> src.Class
            let testCollection = createFrom<_, TestCollection> src.TestCollection
            interface ITestClass with
                member t.Class = klass :> ITypeInfo
                member t.TestCollection = testCollection :> ITestCollection
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestMethod(src: ITestMethod) =
            let mthd = createFrom<_, MethodInfo> src.Method
            let testClass = createFrom<_, TestClass> src.TestClass
            interface ITestMethod with
                member t.Method = mthd :> IMethodInfo
                member t.TestClass = testClass :> ITestClass
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null

        type Xunit1TestCase(test: ITestCase) =
            let displayName = test.DisplayName
            let skipReason = test.SkipReason
            let mutable sourceInformation = createFrom test.SourceInformation
            let testMethod = createFrom<_, TestMethod> test.TestMethod
            let testMethodArguments = test.TestMethodArguments
            let traits = test.Traits
            interface ITestCase with
                member t.DisplayName = displayName
                member t.SkipReason = skipReason
                member t.SourceInformation
                    with get () = sourceInformation :> ISourceInformation
                    and set value = sourceInformation <- (value :?> SourceInformation)
                member t.TestMethod = testMethod :> ITestMethod
                member t.TestMethodArguments = testMethodArguments
                member t.Traits = traits
                member t.UniqueID = invalidOp null
                member t.Serialize(_) = invalidOp null
                member t.Deserialize(_) = invalidOp null

module Message =
    open Transport

    module Worker =
        type Discover = { RunId: string; }
        type Run = { Id: string; Tests: Surrogate.Xunit1TestCase[] }

    module WorkerSet =
        type CoordinatedBy() = class end

    module Coordinator =
        type Start = { Id: string; }
        type Ready = { Workers: IActorRef[] }
        type GetWorkers = { Count: int }

    module Dispatcher =
        type Start = { Client: IActorRef; Id: string }
        type Discovered = { Id: string; Tests: Surrogate.Xunit1TestCase[][] }
        type PartialResult = { Id: string; Result: RemoteRunResult }
        type Continue = { Worker: IActorRef }

    module Client =
        type Started = { Count: int }
        type PartialResult = { Result: RemoteRunResult }
        type Finished = class end

module Worker =
    open Transport
    module Message = Message.Worker

    type private ITestAssemblyFinished with
        member private t.ToSummary() : RunSummary =
            RunSummary(Failed  = t.TestsFailed, Skipped = t.TestsSkipped, Time = t.ExecutionTime, Total = t.TestsFailed + t.TestsRun + t.TestsSkipped)

    type private RunListener() =
        let tests = ResizeArray()
        [<DefaultValue>] val mutable finished: ITestAssemblyFinished

        member t.ToRemoteRunResult() : RemoteRunResult =
            {
                RunSummary = t.finished.ToSummary()
                TestSummaries = tests
            }

        inherit TestMessageVisitor()
            override t.Visit(finish: ITestPassed) : bool =
                tests.Add(TestCaseSummary.Create finish TestResult.Passed)
                base.Visit(finish)

            override t.Visit(skipped: ITestSkipped) : bool =
                tests.Add(TestCaseSummary.Create skipped (TestResult.Skipped skipped.Reason))
                base.Visit(skipped)

            override t.Visit(fail: ITestFailed) : bool =
                tests.Add(TestCaseSummary.Create fail TestResult.Failed)
                base.Visit(fail)

            override t.Visit(finished: ITestAssemblyFinished) : bool =
                t.finished <- finished
                base.Visit(finished)

    type Actor private(builder: IWorkspaceBuilder) as t =
        inherit ReceiveActor()
        do
            base.Receive<Message.Run>(Action<Message.Run>(t.OnReceive))
        static member Create(t) = Actor(t)
        static member Configure (props: Props) = props.WithDispatcher("akka.io.pinned-dispatcher")
        static member Path (id: int) : string = string id
        member private t.OnReceive(msg: Message.Run) =
            let dll = builder.Build(msg.Id)
            let sink = RunListener()
            using (new Xunit.Xunit1(AppDomainSupport.Required, null, dll.FullName)) (fun runner ->
                runner.Run((msg.Tests :> Surrogate.Xunit1TestCase seq) :?> ITestCase seq, sink)
            )
            Actor.Context.Sender.Tell({ Message.Dispatcher.PartialResult.Id = msg.Id; Message.Dispatcher.PartialResult.Result = sink.ToRemoteRunResult() })

module WorkerSet =
    open FireAnt.Akka.FSharp
    module Message = Message.WorkerSet
    open Message

    type Actor private(coordinator: IActorRef, builder: IWorkspaceBuilder) as t =
        inherit ReceiveActor()
        [<DefaultValue>] val mutable children : IActorRef[]
        do
            base.Receive<Message.CoordinatedBy>(Action<Message.CoordinatedBy>(t.OnReceive))
        static member Create(r, t) = Actor(r, t)
        static member Configure (props: Props) = props
        static member Path = "runner"
        override t.PreStart() =
            let context = Actor.Context
            t.children <- Array.init (Environment.ProcessorCount) (fun i -> context.ActorOf(Props.create1<Worker.Actor, _>(builder), Worker.Actor.Path (i + 1)))
            coordinator.Tell({ Message.Coordinator.Ready.Workers = t.children })
        member private t.OnReceive(msg: CoordinatedBy) =
            let context = Actor.Context
            context.Sender.Tell({ Message.Coordinator.Ready.Workers = t.children })

module Dispatcher =
    module Message = Message.Dispatcher
    type DiscoverMessage = Message.Worker.Discover
    type RunMessage = Message.Worker.Run
    open Transport
    open Message

    type Run = { Client: IActorRef; Id: string }
    type State =
        | Initialized
        | WaitForDiscoveryWorker of run: Run
        | WaitForDiscoveryResult of run: Run
        | RunTests of run: Run * finished: ResizeArray<RemoteRunResult> * running: int * waiting: Queue<Surrogate.Xunit1TestCase[]>

        member t.IsFinished =
            match t with
            | RunTests(_, _, 0, waiting) when waiting.Count = 0 -> true
            | _ -> false

        member t.Start(ctx: IActorContext, client: IActorRef, id: string) =
            match t with
            | Initialized ->
                ctx.Parent.Tell({ Coordinator.GetWorkers.Count = 1 })
                State.WaitForDiscoveryWorker({ Client = client; Id = id })
            | _ -> invalidOp null

        member t.ReceiveLocal(ctx: IActorContext, worker: IActorRef) =
            match t with
            | WaitForDiscoveryWorker run ->
                worker.Tell({ DiscoverMessage.RunId = run.Id })
                State.WaitForDiscoveryResult(run)
            | RunTests(run, finished, running, waiting) ->
                if waiting.Count > 0 then
                    worker.Tell({ RunMessage.Id = run.Id; RunMessage.Tests = waiting.Dequeue() })
                    State.RunTests(run, finished, running + 1, waiting)
                else
                    ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| worker |] })
                    invalidOp null
            | _ ->
                ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| worker |] })
                invalidOp null

        member t.ReceiveRemote(ctx: IActorContext, runId: string, tests: Surrogate.Xunit1TestCase[][]) =
            let state = match t with
                        | WaitForDiscoveryResult run when run.Id = runId ->
                            ctx.Parent.Tell({ Message.Coordinator.GetWorkers.Count = tests.Length })
                            State.RunTests(run = run, finished = ResizeArray(), running = 0, waiting = Queue(tests))
                        | _ -> t
            ctx.Parent.Tell({ Message.Coordinator.Ready.Workers = [| ctx.Sender |] })
            state

        member t.ReceiveRemote(ctx: IActorContext, runId: string, result: RemoteRunResult) =
            let state = match t with
                        | RunTests(run, finished, running, waiting) when run.Id = runId ->
                            finished.Add(result)
                            RunTests(run, finished, running - 1, waiting)
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
            let context = Actor.Context
            t.state <- t.state.ReceiveRemote(context, msg.Id, msg.Result)
            if t.state.IsFinished then
                context.Stop(t.Self)

module Coordinator =
    open FireAnt.Akka.FSharp
    module Message = Message.Coordinator
    open Message

    type Actor private(router: IActorRef, timing: ITestTimeRepository) as t =
        inherit ReceiveActor()
        let readyWorkers = HashBag()
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
            let context = Actor.Context
            for worker in msg.Workers do
                if not readyDispatchers.IsEmpty then
                    let dispatcher = readyDispatchers.Pop()
                    dispatcher.Tell({ Message.Dispatcher.Continue.Worker = worker })
                else
                    readyWorkers.Add(worker) |> ignore
                    context.Watch(worker) |> ignore
        member private t.OnReceive(msg: Terminated) =
            let context = Actor.Context
            readyWorkers.Remove(context.Sender)
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