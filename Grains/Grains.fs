namespace FireAnt.Grains

open FireAnt.Interfaces
open Orleans
open Orleans.Placement
open Orleans.Concurrency
open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open Xunit.Sdk

[<ActivationCountBasedPlacement>]
type XunitRemoteTestRunner() =
    inherit Grain()
    interface IRemoteTestRunner with
        member t.RunXunit1(test: Immutable<ITestCase>) : Task<RunSummary> =
            raise <| NotImplementedException()

        member t.RunXunit2(test: Immutable<XunitTestCaseProxy>) : Task<RunSummary> =
            System.Reflection.Assembly.LoadFrom("""D:\Users\vosen\Documents\visual studio 2015\Projects\Ant\DemoLibrary\bin\Debug\DemoLibrary.dll""") |> ignore
            let test = test.Value
            let bus = { new IMessageBus with
                member t.QueueMessage(_) = true
                member t.Dispose() = () }
            let aggregator = ExceptionAggregator()
            let runner = Xunit.Sdk.XunitTestCaseRunner(test, test.DisplayName, test.SkipReason, [||], test.TestMethodArguments, bus, aggregator, new CancellationTokenSource())
            runner.RunAsync()