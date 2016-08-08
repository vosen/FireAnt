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
            for result in msg.Result.TestSummaries do
                if not <| String.IsNullOrEmpty result.Output then
                    logger.Info(sprintf "%s" result.Output)
                match result.Result with
                | TestResult.Error(tests, msgs, stacks) ->
                    logger.Info(sprintf "[ERROR] for [%s]" (String.Join(",", tests)))
                    logger.Info(sprintf "        %s" (String.Join(",", msgs)))
                    logger.Info(sprintf "        %s" (String.Join(",", stacks)))
                | TestResult.Failed name -> logger.Info(sprintf "[FAILURE] for [%s] in %gs" name result.Time)
                | TestResult.Passed name -> logger.Info(sprintf "[SUCCESS] for [%s] in %gs" name result.Time)
                | TestResult.Skipped(name, _) -> logger.Info(sprintf "[SKIPPED] for [%s] in %gs" name result.Time)
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
