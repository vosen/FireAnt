namespace FireAnt.Interfaces

open System
open Orleans.Serialization
open Orleans.CodeGeneration

[<System.Serializable>]
type TestResult = 
    | Passed
    | Failed
    | Skipped of reason: string

[<RegisterSerializer>]
module private TestResultSerializer =
    let DeepCopier(x: obj) : obj = x
    let Serializer (raw: obj) (stream: BinaryTokenStreamWriter ) (_: Type) : unit =
        let result = raw :?> TestResult
        match result with
        | Passed -> stream.Write(1)
        | Failed -> stream.Write(2)
        | Skipped reason -> stream.Write(3); stream.Write(reason)
    let Deserializer (_: Type) (stream : BinaryTokenStreamReader) : obj =
        let tag = stream.ReadInt()
        match tag with
        | 1 -> TestResult.Passed :> obj
        | 2 -> TestResult.Failed :> obj
        | 3 -> TestResult.Skipped(stream.ReadString()) :> obj
        | _ -> invalidArg "tag" :> obj
    SerializationManager.Register(
        typeof<TestResult>,
        SerializationManager.DeepCopier(DeepCopier),
        SerializationManager.Serializer(Serializer),
        SerializationManager.Deserializer(Deserializer))