open System
open System.Linq
open System.Collections.Generic
open System.IO
open System.Threading

open FireAnt
open FireAnt.Akka.FSharp
open FireAnt.Transport
open Akka.Actor
open Akka.Cluster
open Akka.Cluster.Routing
open Akka.Cluster.Tools.Singleton
open Akka.Configuration
open Akka.Routing

type TrivialWorkspaceBuilder(cfg: Config) =
    interface IWorkspaceBuilder with
        member t.Build(runId: string) : FileInfo =
            FileInfo(cfg.GetString("fireant.workspace-path"))

type NullTestTimeRepository() =
    interface ITestTimeRepository with
        member t.GetPredicted(test: TestCase) : decimal option =
            None

type SimpleTestTimeRepository(cfg: Config) =
    let times = 
        let lines = File.ReadAllLines(cfg.GetString("fireant.test-times"))
        lines
        |> Array.map(fun line -> line.Split([| "\t" |], StringSplitOptions.None))
        |> Array.map(fun [| name; time |] -> (name, decimal time))
        |> Map.ofArray
    interface ITestTimeRepository with
        member t.GetPredicted(test: TestCase) : decimal option =
            Map.tryFind (sprintf "%s.%s" test.Type test.Method) times

type TrivialSplitStrategy() =
    static let readOnly (a: 'a[]) : IReadOnlyList<'a> =
        a :> IReadOnlyList<'a>
    interface ISplitStrategy with
        member t.Split(tests: IReadOnlyList<(TestCase * decimal option)>) : IReadOnlyList<IReadOnlyList<TestCase>> =
            tests
            |> (Seq.map (fst >> Array.singleton >> readOnly))
            |> Seq.toArray
            |> readOnly

type SplitBucket(time: decimal, tests: ResizeArray<TestCase>) as t =
    [<DefaultValue>] val mutable Time: decimal
    [<DefaultValue>] val mutable Tests: ResizeArray<TestCase>
    do
        t.Time <- time
        t.Tests <- tests
    member t.Add(time: decimal, test: TestCase) =
        t.Time <- t.Time + time
        t.Tests.Add(test)

type BucketComparer() =
    interface IComparer<SplitBucket> with
        member t.Compare(x, y) =
            x.Time.CompareTo(y.Time)

// Simplest possible greedy split
// We assume here that we we have timings for every test
// This is all well and good for a demo
type SimpleSplitStrategy() =
    static let timeTarget : decimal = 30M
    interface ISplitStrategy with
        member t.Split(tests: IReadOnlyList<(TestCase * decimal option)>) : IReadOnlyList<IReadOnlyList<TestCase>> =
            let totalTime = tests
                            |> Seq.map (snd >> Option.get)
                            |> Seq.sum
            let bucketCount = totalTime / timeTarget
                              |> ceil
                              |> int
            let times = Array.init bucketCount (fun _ -> SplitBucket(0m, ResizeArray()))
            let comparer = BucketComparer()
            for (test, time) in tests do
                times.[0].Add(time.Value, test)
                Array.Sort<_>(times, comparer)
            times |> Array.map (fun (b: SplitBucket) -> b.Tests :> IReadOnlyList<TestCase>) :> IReadOnlyList<IReadOnlyList<TestCase>>

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
