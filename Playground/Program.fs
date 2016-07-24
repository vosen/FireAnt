open Akka.Actor
open Akka.Serialization

[<EntryPoint>]
let main argv =
    let system = ActorSystem.Create("example")
    let serialization = system.Serialization;
    let original = FireAnt.Transport.Xunit1TestCaseProxy("FOO")
    let serializer = serialization.FindSerializerFor(original)
    let bytes = serializer.ToBinary(original)
    let back = serializer.FromBinary(bytes, original.GetType()) :?> FireAnt.Transport.Xunit1TestCaseProxy
    printfn "%A" bytes
    printfn "%A" back
    System.Console.ReadLine()
    0