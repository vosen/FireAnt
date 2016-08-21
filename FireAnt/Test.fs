namespace FireAnt

module Test =
    open System.IO.Pipes
    open Akka.Actor
    open System.Diagnostics
    open System.IO
    open FireAnt.Transport
    open System.Collections.Generic
    open FSharp.Control

    [<Literal>]
    let FireAntRunner = "FireAnt.Xunit.Runner.exe"

    let EmptyBuffer = [||]

    let rec AsyncWhile (predicateFn : unit -> Async<bool>) (action : Async<unit>) : Async<unit> = 
        async {
            let! b = predicateFn()
            if b then
                do! action
                return! AsyncWhile predicateFn action
        }

    let rec AsyncSeqWhile (predicateFn : unit -> Async<bool>) (action : AsyncSeq<'a>) : AsyncSeq<'a> = 
        asyncSeq {
            let! b = predicateFn()
            if b then
                yield! action
                yield! AsyncSeqWhile predicateFn action
        }

    type AsyncBuilder with
        member t.While(cond, body) = AsyncWhile cond body

    type AsyncSeq.AsyncSeqBuilder with
        member t.While(cond, body) = AsyncSeqWhile cond body

    type NamedPipeServerStream with
        member t.WaitForData() : Async<bool> = async {
            let! count = t.AsyncRead([| 0uy |], 0, 1)
            return count > 0 && t.IsConnected
        }

    let CopyXunitRunner(dll: FileInfo) : string =
        let target = Path.Combine(dll.Directory.FullName, FireAntRunner)
        if not <| File.Exists(target) then
            File.Copy(FireAntRunner, target)
        let originalManifest = sprintf "%s.manifest" dll.FullName
        if File.Exists(originalManifest) then
            let manifest = sprintf "%s.manifest" target
            if not <| File.Exists(manifest) then
                File.Copy(originalManifest, manifest)
        let originalConfig = sprintf "%s.config" dll.FullName
        if File.Exists(originalConfig) then
            let config = sprintf "%s.config" target
            if not <| File.Exists(config) then
                File.Copy(originalConfig, config)
        target

    let Discover (path: ActorPath, workspaceBuilder: unit -> FileInfo) =
        let cases = ResizeArray()
        async {
            let dll = workspaceBuilder()
            let pipeAddress = sprintf "FireAnt%s/discovery" <| path.ToStringWithoutAddress()
            use server = new NamedPipeServerStream(pipeAddress, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)
            let runnerPath = CopyXunitRunner(dll)
            let psi = ProcessStartInfo(runnerPath)
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true
            psi.Arguments <- sprintf "discovery \"%s\" \"%s\"" pipeAddress dll.FullName
            use proc = Process.Start(psi)
            server.WaitForConnection()
            while server.WaitForData() do
                cases.Add(TestCase.ParseDelimitedFrom(server))
            proc.Close()
            server.Disconnect()
            return cases
        }

    let Run (path: ActorPath, workspaceBuilder: unit -> FileInfo, tests: IReadOnlyList<TestCase>) =
        asyncSeq {
            let dll = workspaceBuilder()
            let pipeAddress = sprintf "FireAnt%s/run" <| path.ToStringWithoutAddress()
            use server = new NamedPipeServerStream(pipeAddress, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)
            let runnerPath = CopyXunitRunner(dll)
            let psi = ProcessStartInfo(runnerPath)
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true
            psi.Arguments <- sprintf "run \"%s\" \"%s\" %s" pipeAddress dll.FullName (String.concat " " (tests |> Seq.map (fun case -> sprintf "\"%s\" \"%s\"" case.Type case.Method)))
            use proc = Process.Start(psi)
            server.WaitForConnection()
            while server.WaitForData() do
                yield TestResultSummary.ParseDelimitedFrom(server)
            proc.Close()
            server.Disconnect()
        }
