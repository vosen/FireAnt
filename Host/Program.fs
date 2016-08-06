open System
open System.Collections.Generic
open System.IO
open System.Threading

open FireAnt
open FireAnt.Akka.FSharp
open Akka.Actor
open Akka.Cluster
open Akka.Cluster.Routing
open Akka.Cluster.Tools.Singleton
open Akka.Configuration
open Akka.Routing
open Xunit.Abstractions

type TrivialWorkspaceBuilder(cfg: Config) =
    interface IWorkspaceBuilder with
        member t.Build(runId: string) : FileInfo =
            FileInfo(cfg.GetString("workspace.path"))

type NullTestTimeRepository() =
    interface ITestTimeRepository with
        member t.GetPredicted(name: string) : decimal option =
            None

type TrivialSplitStrategy() =
    static let readOnly (a: 'a[]) : IReadOnlyList<'a> =
        a :> IReadOnlyList<'a>
    interface ISplitStrategy with
        member t.Split(tests: IReadOnlyList<(ITestCase * decimal option)>) : IReadOnlyList<IReadOnlyList<ITestCase>> =
            tests
            |> (Seq.map (fst >> Array.singleton >> readOnly))
            |> Seq.toArray
            |> readOnly

[<EntryPoint>]
let main argv =
    using(ActorSystem.Create("FireAnt")) (fun system ->
        let clusterBroadcastTo (path: string) =
            let path = [| path |]
            system.ActorOf(Props.Empty.WithRouter(ClusterRouterGroup(BroadcastGroup(path), ClusterRouterGroupSettings(1000, path, true))))
        let broadcastToWorkerSet = clusterBroadcastTo(sprintf "/user/%s" WorkerSet.Actor.Path)
        system.ActorOf(ClusterSingletonManager.Props(Props.create2<Coordinator.Actor, _, _>(broadcastToWorkerSet, NullTestTimeRepository()), ClusterSingletonManagerSettings.Create(system)), Coordinator.Actor.Path) |> ignore
        let proxy = system.ActorOf(ClusterSingletonProxy.Props("/user/"+ Coordinator.Actor.Path, ClusterSingletonProxySettings.Create(system)))
        System.actorOf4<WorkerSet.Actor, _, _, _, _>(system, proxy, TrivialWorkspaceBuilder(system.Settings.Config), NullTestTimeRepository(), TrivialSplitStrategy()) |> ignore
        let cluster = Cluster.Get(system)
        while Console.ReadKey(true).Key <> ConsoleKey.Escape do ()
        use leftCluster = new ManualResetEventSlim()
        cluster.RegisterOnMemberRemoved(fun () -> leftCluster.Set())
        cluster.Leave(cluster.SelfAddress)
        leftCluster.Wait()
    )
    0
