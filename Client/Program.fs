open System

open FireAnt
open FireAnt.Akka.FSharp
open FireAnt.Transport
open Akka.Actor
open Akka.Event
open Akka.Cluster
open Akka.Cluster.Tools.Singleton
open System.Threading

module Client =
    open FireAnt.Message

    type Actor private(proxy: IActorRef) as t =
        inherit ReceiveActor()
        do
            base.Receive<Client.Started>(Action<Client.Started>(t.OnReceive))
            base.Receive<ClusterEvent.MemberUp>(Action<ClusterEvent.MemberUp>(t.OnReceive))
            base.Receive<Client.PartialResult>(Action<Client.PartialResult>(t.OnReceive))
            base.Receive<Client.Finished>(Action<Client.Finished>(t.OnReceive))
        let logger = Logging.GetLogger(Actor.Context)
        static member Create(p) = Actor(p)
        static member Configure (props: Props) = props
        static member Path = "client"
        member t.OnReceive(msg: Client.Started) =
            logger.Info("[STARTED]")
        member t.OnReceive(msg: ClusterEvent.MemberUp) =
            if msg.Member.Address = Cluster.Get(Actor.Context.System).SelfAddress then
                proxy.Tell({ Message.Coordinator.Start.Id = "" })
        member t.OnReceive(msg: Client.PartialResult) =
            let result = msg.Result
            match result.Result with
            | TestResult.Pass -> logger.Info(sprintf "[SUCCESS] for [%s] in %gs" result.DisplayName result.Time)
            | TestResult.Fail -> logger.Info(sprintf "[FAILURE] for [%s] in %gs:\n%s: %s\n%s" result.DisplayName result.Time result.ExceptionType result.ExceptionMessage result.ExceptionMessage)
            | TestResult.Skip -> logger.Info(sprintf "[SKIPPED] for [%s]" result.DisplayName)
        member t.OnReceive(msg: Client.Finished) =
            logger.Info("[FINISHED]")

[<EntryPoint>]
let main _ =
    using(ActorSystem.Create("FireAnt")) (fun system ->
        let proxy = system.ActorOf(ClusterSingletonProxy.Props("/user/"+ Coordinator.Actor.Path, ClusterSingletonProxySettings.Create(system)))
        let client = System.actorOf1<Client.Actor, _> (system, proxy)
        let cluster = Cluster.Get(system)
        cluster.Subscribe(client, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents, [| typeof<ClusterEvent.MemberUp> |])
        while Console.ReadKey(true).Key <> ConsoleKey.Escape do ()
        use leftCluster = new ManualResetEventSlim()
        cluster.RegisterOnMemberRemoved(fun () -> leftCluster.Set())
        cluster.Leave(cluster.SelfAddress)
        leftCluster.Wait()
    )
    0
