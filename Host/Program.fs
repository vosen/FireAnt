open System
open System.IO
open System.Threading

open FireAnt
open Akka.Actor
open Akka.Routing

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

type FuncActorProducer<'a when 'a :> ActorBase>(f: unit -> 'a) =
    interface IIndirectActorProducer with
        member x.ActorType = typeof<'a>
        member x.Produce() = f() :> ActorBase
        member x.Release(_) = ()

type RunnerProducer() =
    inherit FuncActorProducer<TestRunner.Actor>(fun () -> TestRunner.Actor(NullWorkspaceBuilder()))

type DispatcherProducer() =
    inherit FuncActorProducer<TestDispatcher.Actor>(fun () -> TestDispatcher.Actor(NullTestTimeRepository()))

[<EntryPoint>]
let main argv =
    using(ActorSystem.Create("FireAnt")) (fun system ->
        Array.init (Environment.ProcessorCount) (fun i -> system.ActorOf(Props.CreateBy<RunnerProducer>() |> TestRunner.Actor.Configure, TestRunner.Actor.Path (i + 1))) |> ignore
        system.ActorOf(Props.CreateBy<DispatcherProducer>() |> TestDispatcher.Actor.Configure, TestDispatcher.Actor.Path) |> ignore
        while Console.ReadKey().Key <> ConsoleKey.Escape do ()
    )
    0
