namespace FireAnt.Akka

open Akka
open Akka.Actor
open System

module FSharp =
    type FuncActorProducer<'a when 'a :> ActorBase>(f: unit -> 'a) =
        interface IIndirectActorProducer with
            member x.ActorType = typeof<'a>
            member x.Produce() = f() :> ActorBase
            member x.Release(actor) =
                match actor :> obj with
                | :? IDisposable as actor -> actor.Dispose()
                | _ -> ()

    type IInternalActor with
        member inline t.ActorOf< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)>() =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit ->  ^T) ()))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U ->  ^T) u))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U, v: ^V) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V ->  ^T) (u,v)))), (^T: (static member get_Path: unit -> string) ()))

    module System =
        let inline actorOf< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit ->  ^T) ()))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf1< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U ->  ^T) u))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf2< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U, v: ^V) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V ->  ^T) (u,v)))), (^T: (static member get_Path: unit -> string) ()))

    module Props =
        let inline create< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props)> =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit ->  ^T) ())))

        let inline create1< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U ->  ^T) u)))

        let inline create2< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U, v: ^V) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V  ->  ^T) (u,v))))
