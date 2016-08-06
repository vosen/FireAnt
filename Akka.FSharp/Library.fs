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

    [<AbstractClass>]
    type AsyncActor() =
        inherit ReceiveActor()
        // for some reason  this trips up type inference
        // if defined and used in the same actor type
        member t.ReceiveRespond<'T, 'U>(f: 'T * ('U -> unit) -> unit) =
            t.Receive<'T>(Action<'T>(fun a ->
                let ctx = (t :> IInternalActor).ActorContext
                let self = ctx.Self
                let sender = ctx.Sender
                f (a, (fun b -> self.Tell((sender, b))))))
            t.Receive<(IActorRef * 'U)>(Action<(IActorRef * 'U)>(fun (sender, b) -> sender.Tell(b)))

    type IInternalActor with
        member inline t.ActorOf< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)>() =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit ->  ^T) ()))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U ->  ^T) u))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U, v: ^V) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V ->  ^T) (u,v)))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U, ^V, ^W when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U, v: ^V, w: ^W) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W ->  ^T) (u,v,w)))), (^T: (static member get_Path: unit -> string) ()))

        member inline t.ActorOf< ^T, ^U, ^V, ^W, ^X  when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W * ^X  ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (u: ^U, v: ^V, w: ^W, x: ^X) =
            t.ActorContext.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W * ^X ->  ^T) (u,v,w,x)))), (^T: (static member get_Path: unit -> string) ()))

    module System =
        let inline actorOf< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit ->  ^T) ()))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf1< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U ->  ^T) u))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf2< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U, v: ^V) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V ->  ^T) (u,v)))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf3< ^T, ^U, ^V, ^W when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U, v: ^V, w: ^W) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W ->  ^T) (u,v,w)))), (^T: (static member get_Path: unit -> string) ()))

        let inline actorOf4< ^T, ^U, ^V, ^W, ^X when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W * ^X ->  ^T) and ^T: (static member Configure : Props -> Props) and ^T: (static member get_Path: unit -> string)> (system: ActorSystem, u: ^U, v: ^V, w: ^W, x: ^X) =
            system.ActorOf((^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W * ^X ->  ^T) (u,v,w,x)))), (^T: (static member get_Path: unit -> string) ()))

    module Props =
        let inline create< ^T when ^T :> ActorBase and ^T : (static member Create: unit ->  ^T) and ^T: (static member Configure : Props -> Props)> =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: unit -> ^T) ())))

        let inline create1< ^T, ^U when ^T :> ActorBase and ^T : (static member Create: ^U ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U -> ^T) u)))

        let inline create2< ^T, ^U, ^V when ^T :> ActorBase and ^T : (static member Create: ^U * ^V ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U, v: ^V) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V -> ^T) (u,v))))

        let inline create3< ^T, ^U, ^V, ^W when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U, v: ^V, w: ^W) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W -> ^T) (u,v,w))))

        let inline create4< ^T, ^U, ^V, ^W, ^X when ^T :> ActorBase and ^T : (static member Create: ^U * ^V * ^W * ^X ->  ^T) and ^T: (static member Configure : Props -> Props)> (u: ^U, v: ^V, w: ^W, x: ^X) =
            (^T: (static member Configure : Props -> Props) Props.CreateBy<FuncActorProducer< ^T>>(fun () -> (^T: (static member Create: ^U * ^V * ^W * ^X -> ^T) (u,v,w,x))))
