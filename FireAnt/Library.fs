namespace FireAnt
#nowarn "686" // suppress warnings about createFrom

open Akka
open Akka.Actor

open Xunit
open Xunit.Abstractions
open Xunit.Sdk

open System
open System.IO
open System.Collections.Generic

type IWorkspaceBuilder =
    abstract member Build: string -> FileInfo

type ITestTimeRepository =
    abstract member GetPredicted: string -> decimal
    abstract member Store: runId: string * test: string * time: decimal -> unit

module Transport =
    type TestResult = 
    | Passed
    | Failed
    | Skipped of reason: string

    type TestCaseSummary =
     {
        Name: string
        Result: TestResult
        Time: decimal
        Output: string
     }
     with
        static member Create (output: ITestResultMessage) (result: TestResult) : TestCaseSummary =
            {
                Name = output.TestCase.DisplayName
                Result = result
                Time = output.ExecutionTime
                Output = output.Output
            }

    type RemoteRunResult =
        {
            RunSummary: RunSummary
            TestSummaries: IReadOnlyList<TestCaseSummary>
        }

    module Surrogate =
        let inline createFrom x = if isNull x then null else (^T : (new : ^U -> ^T) x)

        [<AllowNullLiteral>]
        type SourceInformation(src: ISourceInformation) =
            let mutable fileName = src.FileName
            let mutable lineNumber = src.LineNumber
            interface ISourceInformation with
                member t.FileName
                    with get () = fileName
                    and set value = fileName <- value
                member t.LineNumber
                    with get () = lineNumber
                    and set value = lineNumber <- value
                member t.Serialize(_) = invalidOp null
                member t.Deserialize(_) = invalidOp null

        [<AllowNullLiteral>]
        type AssemblyInfo(assembly: IAssemblyInfo) =
            let assemblyPath = assembly.AssemblyPath
            let name = assembly.Name
            interface IAssemblyInfo with
                member t.AssemblyPath = assemblyPath
                member t.Name = name
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetType(_) = invalidOp null
                member t.GetTypes(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TypeInfo(typ: ITypeInfo) =
            let assembly = createFrom<_, AssemblyInfo> typ.Assembly
            let baseType = null // This is slightly wrong, but otherwise we'de have to serialize all the inheritance path
            let interfaces = typ.Interfaces
                             |> Seq.map (fun i -> createFrom<_, TypeInfo> i :> ITypeInfo)
                             |> ResizeArray
            let isAbstract = typ.IsAbstract;
            let isGenericParameter = typ.IsGenericParameter
            let isGenericType = typ.IsGenericType
            let isSealed = typ.IsSealed
            let isValueType = typ.IsValueType
            let name = typ.Name
            interface ITypeInfo with                
                member t.Assembly = assembly :> IAssemblyInfo
                member t.BaseType = baseType
                member t.Interfaces = interfaces :> ITypeInfo seq
                member t.IsAbstract = isAbstract
                member t.IsGenericParameter = isGenericParameter
                member t.IsGenericType = isGenericType
                member t.IsSealed = isSealed
                member t.IsValueType = isValueType
                member t.Name = name
                member t.GetMethod(_, _) = invalidOp null
                member t.GetMethods(_) = invalidOp null
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetGenericArguments() = invalidOp null

        [<AllowNullLiteral>]
        type MethodInfo(minfo: IMethodInfo) =
            let isAbstract = minfo.IsAbstract;
            let isGenericMethodDefinition = minfo.IsGenericMethodDefinition;
            let isPublic = minfo.IsPublic;
            let isStatic = minfo.IsStatic;
            let name = minfo.Name;
            let returnType = createFrom<_, TypeInfo> minfo.ReturnType
            let typ = createFrom<_, TypeInfo> minfo.Type
            interface IMethodInfo with
                member t.IsAbstract = isAbstract
                member t.IsGenericMethodDefinition = isGenericMethodDefinition
                member t.IsPublic = isPublic
                member t.IsStatic = isStatic
                member t.Name = name
                member t.ReturnType = returnType :> ITypeInfo
                member t.Type = typ :> ITypeInfo
                member t.GetCustomAttributes(_) = invalidOp null
                member t.GetGenericArguments() = invalidOp null
                member t.GetParameters() = invalidOp null
                member t.MakeGenericMethod(_) = invalidOp null

        [<AllowNullLiteral>]
        type TestAssembly(src: ITestAssembly) =
            let assembly = createFrom<_, AssemblyInfo> src.Assembly
            let configFileName = src.ConfigFileName
            interface ITestAssembly with
                member t.Assembly = assembly :> IAssemblyInfo
                member t.ConfigFileName = configFileName
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestCollection(src: ITestCollection) =
            let collectionDefinition = createFrom<_, TypeInfo> src.CollectionDefinition
            let displayName = src.DisplayName
            let testAssembly = createFrom<_, TestAssembly> src.TestAssembly
            interface ITestCollection with
                member t.CollectionDefinition = collectionDefinition :> ITypeInfo
                member t.DisplayName = displayName
                member t.TestAssembly = testAssembly :> ITestAssembly
                member t.UniqueID =  invalidOp null
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestClass(src: ITestClass) =
            let klass = createFrom<_, TypeInfo> src.Class
            let testCollection = createFrom<_, TestCollection> src.TestCollection
            interface ITestClass with
                member t.Class = klass :> ITypeInfo
                member t.TestCollection = testCollection :> ITestCollection
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null
                

        [<AllowNullLiteral>]
        type TestMethod(src: ITestMethod) =
            let mthd = createFrom<_, MethodInfo> src.Method
            let testClass = createFrom<_, TestClass> src.TestClass
            interface ITestMethod with
                member t.Method = mthd :> IMethodInfo
                member t.TestClass = testClass :> ITestClass
                member t.Deserialize(_) = invalidOp null
                member t.Serialize(_) = invalidOp null

        type Xunit1TestCase(test: ITestCase) =
            let displayName = test.DisplayName
            let skipReason = test.SkipReason
            let mutable sourceInformation = createFrom test.SourceInformation
            let testMethod = createFrom<_, TestMethod> test.TestMethod
            let testMethodArguments = test.TestMethodArguments
            let traits = test.Traits
            interface ITestCase with
                member t.DisplayName = displayName
                member t.SkipReason = skipReason
                member t.SourceInformation
                    with get () = sourceInformation :> ISourceInformation
                    and set value = sourceInformation <- (value :?> SourceInformation)
                member t.TestMethod = testMethod :> ITestMethod
                member t.TestMethodArguments = testMethodArguments
                member t.Traits = traits
                member t.UniqueID = invalidOp null
                member t.Serialize(_) = invalidOp null
                member t.Deserialize(_) = invalidOp null

module TestRunner =
    open Transport

    type private ITestAssemblyFinished with
        member private t.ToSummary() : RunSummary =
            RunSummary(Failed  = t.TestsFailed, Skipped = t.TestsSkipped, Time = t.ExecutionTime, Total = t.TestsFailed + t.TestsRun + t.TestsSkipped)

    type private RunListener() =
        let tests = ResizeArray()
        [<DefaultValue>] val mutable finished: ITestAssemblyFinished

        member t.ToRemoteRunResult() : RemoteRunResult =
            {
                RunSummary = t.finished.ToSummary()
                TestSummaries = tests
            }

        inherit TestMessageVisitor()
            override t.Visit(finish: ITestPassed) : bool =
                tests.Add(TestCaseSummary.Create finish TestResult.Passed)
                base.Visit(finish)

            override t.Visit(skipped: ITestSkipped) : bool =
                tests.Add(TestCaseSummary.Create skipped (TestResult.Skipped skipped.Reason))
                base.Visit(skipped)

            override t.Visit(fail: ITestFailed) : bool =
                tests.Add(TestCaseSummary.Create fail TestResult.Failed)
                base.Visit(fail)

            override t.Visit(finished: ITestAssemblyFinished) : bool =
                t.finished <- finished
                base.Visit(finished)

    module Message =
        type Run = { RunId: string; Sender: IActorRef; Tests: Surrogate.Xunit1TestCase[] }
        type SendPartialResult = { Result: Transport.RemoteRunResult }

    type Actor(builder: IWorkspaceBuilder) as t =
        inherit ReceiveActor()
        do
            base.Receive<Message.Run>(Action<Message.Run>(t.OnReceive))
        static member Path (id: int) : string = sprintf "runner%d" id
        static member Configure (props: Props) = props.WithDispatcher("akka.io.pinned-dispatcher")
        member private t.OnReceive(msg: Message.Run) =
            let dll = builder.Build(msg.RunId)
            let sink = RunListener()
            using (new Xunit.Xunit1(AppDomainSupport.Required, null, dll.FullName)) (fun runner ->
                runner.Run((msg.Tests :> Surrogate.Xunit1TestCase seq) :?> ITestCase seq, sink)
            )
            msg.Sender.Tell({ Message.SendPartialResult.Result = sink.ToRemoteRunResult() })

module TestDispatcher =
    module Message =
        type Run = { RunId: string }
        type PartialResult = TestRunner.Message.SendPartialResult
        type RunFinished = { RunId: string }

    type Actor(timing: ITestTimeRepository) as t =
        inherit ReceiveActor()
        do
            base.Receive<Message.Run>(Action<Message.Run>(t.OnReceive))
            base.Receive<Message.PartialResult>(Action<Message.PartialResult>(t.OnReceive))
        static member Path : string = "dispatcher"
        static member Configure (props: Props) = props
        member private t.OnReceive(msg: Message.Run) =
            ()
        member private t.OnReceive(msg: Message.PartialResult) =
            ()