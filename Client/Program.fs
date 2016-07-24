open System

open FireAnt.Transport
open FireAnt.TestDispatcher
open FireAnt.TestDispatcher.Message
open Akka.Actor
open Akka.Cluster
open System.Threading

type ClientActor(address: Address, router: IActorRef, finished: ManualResetEventSlim) as t =
    inherit ReceiveActor()
    do
        base.Receive<ClusterEvent.MemberUp>(Action<ClusterEvent.MemberUp>(t.OnReceive))
        base.Receive<Message.PartialResult>(Action<Message.PartialResult>(t.OnReceive))
        base.Receive<Message.RunFinished>(Action<Message.RunFinished>(t.OnReceive))
    member t.Finished = finished
    member t.OnReceive(msg: ClusterEvent.MemberUp) =
        if msg.Member.Address = address then
            router.Tell({ Run.RunId = "" })
    member t.OnReceive(msg: Message.PartialResult) =
        let result = msg.Result.TestSummaries.[0]
        printfn "%s" result.Output
        let output = match result.Result with
                     | TestResult.Failed -> "FAILURE"
                     | TestResult.Passed -> "SUCCESS"
                     | TestResult.Skipped _ -> "SKIPPED"
        printfn "[%s] for [%s] in %gs" output result.Name result.Time
    member t.OnReceive(msg: Message.RunFinished) =
        printfn "[FINISHED]"
        finished.Set()

type DeadLettersActor() as t =
    inherit ReceiveActor()
    do
        base.Receive<Akka.Event.DeadLetter>(Action<Akka.Event.DeadLetter>(t.OnReceive))
    member t.OnReceive(msg: Akka.Event.DeadLetter) =
        printfn "[DEAD LETTER]"
        printfn "%A" msg.Message

[<EntryPoint>]
let main argv =
    let task = using(ActorSystem.Create("FireAnt")) (fun system ->
        system.EventStream.Subscribe(system.ActorOf(Props.Create<DeadLettersActor>()), typeof<Akka.Event.DeadLetter>) |> ignore
        use finished = new ManualResetEventSlim()
        let cluster = Cluster.Get(system)
        let router = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.FromConfig.Instance), "dispatcher-router")
        let client = system.ActorOf(Props.Create<ClientActor>(cluster.SelfAddress, router, finished))
        cluster.Subscribe(client, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents, [| typeof<ClusterEvent.MemberUp> |])
        while Console.ReadKey().Key <> ConsoleKey.Escape do ()
    )
    0
