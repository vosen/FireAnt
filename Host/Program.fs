open System
open System.IO
open System.Threading

open FireAnt
open FireAnt.Akka.FSharp
open Akka.Actor
open Akka.Cluster.Routing
open Akka.Routing
open Akka.Cluster.Tools.Singleton

type NullTestTimeRepository() =
    interface ITestTimeRepository with
        member t.GetPredicted(name: string) : decimal =
            0m
        member t.Store(runId: string, test: string, time: decimal) : unit =
            ()

type NullWorkspaceBuilder() =
    interface IWorkspaceBuilder with
        member t.Build(runId: string) : FileInfo =
            FileInfo("NUL")

[<EntryPoint>]
let main argv =
    using(ActorSystem.Create("FireAnt")) (fun system ->
        let clusterBroadcastTo (path: string) =
            let path = [| path |]
            system.ActorOf(Props.Empty.WithRouter(ClusterRouterGroup(BroadcastGroup(path), ClusterRouterGroupSettings(1, path, true))))
        let broadcastToWorkerSet = clusterBroadcastTo(sprintf "/user/%s" WorkerSet.Actor.Path)
        system.ActorOf(ClusterSingletonManager.Props(Props.create2<Coordinator.Actor, _, _>(broadcastToWorkerSet, NullTestTimeRepository()), ClusterSingletonManagerSettings.Create(system)), Coordinator.Actor.Path) |> ignore
        let proxy = system.ActorOf(ClusterSingletonProxy.Props("/user/"+ Coordinator.Actor.Path, ClusterSingletonProxySettings.Create(system)))
        System.actorOf2<WorkerSet.Actor, _, _> system (proxy, NullWorkspaceBuilder()) |> ignore
        while Console.ReadKey().Key <> ConsoleKey.Escape do ()
    )
    0
