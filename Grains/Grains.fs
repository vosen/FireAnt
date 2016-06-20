namespace FireAnt.Grains

open FireAnt.Interfaces
open Orleans
open Orleans.Placement
open Orleans.Concurrency
open System
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open Xunit.Sdk

[<ActivationCountBasedPlacement>]
type XunitTestRunner() =
    inherit Grain()
    interface IRemoteTestRunner with
        member t.RunXunit1(test: Immutable<ITestCase>): Task<RunSummary> =
            raise <| NotImplementedException()

        member t.RunXunit2(test: Immutable<IXunitTestCase>): Task<RunSummary> =
            raise <| NotImplementedException()