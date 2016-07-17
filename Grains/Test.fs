namespace FireAnt.Grains

module Tests =
    open System.IO
    open Xunit
    open Xunit.Abstractions
    open Xunit.Sdk

    type FindListener() =
        inherit TestMessageVisitor()
        let tests = ResizeArray()
        member t.Tests = tests
        override t.Visit(test: ITestCaseDiscoveryMessage) : bool =
            tests.Add(test.TestCase)
            base.Visit(test)

    let Discover (dll: FileInfo) : ResizeArray<ITestCase> =
        let listener = FindListener()
        using (new Xunit.Xunit1(AppDomainSupport.Required, null, dll.FullName)) (fun runner ->
            runner.Find(false, listener)
        )
        listener.Tests