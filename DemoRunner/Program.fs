open FireAnt.Interfaces
open Orleans
open Orleans.Concurrency
open Orleans.Runtime.Configuration
open System
open System.Threading
open Xunit
open Xunit.Abstractions
open Xunit.Sdk

type Visitor() =
    inherit TestMessageVisitor()

    member val Finished = new ManualResetEventSlim()

    interface IDisposable with
        member t.Dispose() = t.Finished.Dispose()

    override t.Visit(discoveryComplete: IDiscoveryCompleteMessage) =
        let value = base.Visit(discoveryComplete)
        t.Finished.Set()
        value

    override t.Visit(discovered: ITestCaseDiscoveryMessage) =
        let value = base.Visit(discovered)
        let grain = GrainClient.GrainFactory.GetGrain<IRemoteTestRunner>(Guid.NewGuid())
        let result = grain.RunXunit2(Immutable(discovered.TestCase :?> IXunitTestCase)).Result
        value

[<EntryPoint>]
let main argv =
    GrainClient.Initialize(ClientConfiguration.LocalhostSilo());
    using (new Visitor()) (fun visitor ->
        let controller = new XunitFrontController(AppDomainSupport.Denied,
                                                  @"D:\Users\vosen\Documents\visual studio 2015\Projects\Ant\DemoLibrary\bin\Debug\DemoLibrary.dll",
                                                  shadowCopy= false)
        using (controller) (fun controller ->
            let conf = TestAssemblyConfiguration()
            conf.PreEnumerateTheories <- Nullable(false)
            controller.Find(false, visitor, TestFrameworkOptions.ForDiscovery(conf))
            visitor.Finished.Wait()
        )
    )
    0 // return an integer exit code
