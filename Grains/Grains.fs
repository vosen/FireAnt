namespace FireAnt.Grains

open FireAnt.Interfaces
//open FSharpx.Task
open Microsoft.FSharp.Collections
open Orleans
open Orleans.Concurrency
open Orleans.Placement
open Orleans.Streams
open System
open System.Collections.Generic
open System.IO
open System.Threading
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open Xunit.Sdk
open System.Reflection

type IWorkspaceBuilder =
    abstract member Build: string -> FileInfo

type ITestTimeRepository =
    abstract member GetPredicted: string -> int
    abstract member Store: runId: string * test: string * time: decimal -> unit

module Grains =
    module Array =
        let singleton x = [| x |]

    module ResizeArray =
        let map f (x: ResizeArray<'a>) : ResizeArray<'b> =
            let len = x.Count
            let ret = new ResizeArray<_>(len)
            for i = 0 to len - 1 do
                ret.Add(f x.[i])
            ret

        let mapToArray f (x: ResizeArray<'a>) : 'b[] =
            let len = x.Count
            let a = Array.zeroCreate x.Count
            for i = 0 to len - 1 do
                a.[i] <- f x.[i]
            a

    type ITestAssemblyFinished with
        member t.ToSummary() : RunSummary =
            RunSummary(Failed  = t.TestsFailed, Skipped = t.TestsSkipped, Time = t.ExecutionTime, Total = t.TestsFailed + t.TestsRun + t.TestsSkipped)

    let inline toTestCaseSummary (ouput: ITestResultMessage) (result: TestResult) : TestCaseSummary =
        TestCaseSummary(result, ouput.ExecutionTime, ouput.Output)

    type RunListener() =
        let tests = ResizeArray()
        [<DefaultValue>] val mutable finished: ITestAssemblyFinished

        member t.ToRemoteRunResult() : RemoteRunResult =
            let result = RemoteRunResult(t.finished.ToSummary(), tests)
            result

        inherit TestMessageVisitor()
            override t.Visit(finish: ITestPassed) : bool =
                tests.Add(toTestCaseSummary finish TestResult.Passed)
                base.Visit(finish)

            override t.Visit(skipped: ITestSkipped) : bool =
                tests.Add(toTestCaseSummary skipped (TestResult.Skipped skipped.Reason))
                base.Visit(skipped)

            override t.Visit(fail: ITestFailed) : bool =
                tests.Add(toTestCaseSummary fail TestResult.Failed)
                base.Visit(fail)

            override t.Visit(finished: ITestAssemblyFinished) : bool =
                t.finished <- finished
                base.Visit(finished)


    [<ActivationCountBasedPlacement>]
    type XunitRemoteTestRunner(wsBuilder: IWorkspaceBuilder) =
        inherit Grain()
        interface IRemoteTestRunner with
            member t.RunXunit1(runId: string, tests: Immutable<Xunit1TestCaseProxy[]>) : Task<Immutable<RemoteRunResult>> =
                let builder = Task.TaskBuilder(scheduler = TaskScheduler.Current)
                builder {
                    let dll = wsBuilder.Build(runId)
                    let sink = RunListener()
                    using (new Xunit.Xunit1(AppDomainSupport.Required, null, dll.FullName)) (fun runner ->
                        runner.Run((tests.Value :> Xunit1TestCaseProxy seq) :?> ITestCase seq, sink)
                    )
                    return sink.ToRemoteRunResult().AsImmutable()
                }

    [<ActivationCountBasedPlacement>]
    type RemoteTestDispatcher(wsBuilder: IWorkspaceBuilder, timing: ITestTimeRepository) =
        inherit Grain()
        let splitIntoGroups (tests: ResizeArray<ITestCase>) : ResizeArray<ITestCase[]> =
            ResizeArray.map (Array.singleton) tests

        let sendToRunners (runId: string) (tests: ResizeArray<ITestCase[]>) : Task<Immutable<RemoteRunResult>>[] =
            tests
            |> ResizeArray.mapToArray (fun tests ->
                   let runner = GrainClient.GrainFactory.GetGrain<IRemoteTestRunner>(Guid.NewGuid())
                   let wrappedTests = Array.map (fun t -> Xunit1TestCaseProxy(t)) tests
                   runner.RunXunit1(runId, wrappedTests.AsImmutable())
               )
        let broadcastPartialResults (runId: string) (stream: IAsyncStream<RemoteRunResult>) (groups: ResizeArray<ITestCase[]>) (runTasks: Task<Immutable<RemoteRunResult>>[]) : Task =
            let onFinishedTest (stream: IAsyncStream<RemoteRunResult>) (tests: ITestCase[]) (results: Task<Immutable<RemoteRunResult>>) =
                let results = results.Result.Value
                (tests, results.TestSummaries)
                ||> Seq.zip
                |> Seq.iter (fun (test, summary) -> timing.Store(runId, test.DisplayName, summary.Time))
                stream.OnNextAsync(results)
            let bcastTasks = runTasks |> Array.mapi (fun i t -> t.ContinueWith(onFinishedTest stream groups.[i]))
            Task.Factory.ContinueWhenAll(bcastTasks, (fun _ -> stream.OnCompletedAsync())).Unwrap()

        member private t.CreateStream() : IAsyncStream<RemoteRunResult> =
            let streamProvider = t.GetStreamProvider("SMSProvider")
            streamProvider.GetStream(t.GetPrimaryKey(), null)

        interface IRemoteTestDispatcher with
            member t.Run(testId: string) : Task =
                let stream = t.CreateStream()
                let builder = Task.TaskBuilder(scheduler = TaskScheduler.Current)
                let task = builder {
                    let groups = wsBuilder.Build testId
                                 |> Tests.Discover
                                 |> splitIntoGroups
                    return sendToRunners testId groups
                           |> broadcastPartialResults testId stream groups
                }
                task.Unwrap()