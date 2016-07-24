open System
open System.Threading
open Akka.FSharp
open Akka.Actor

let MyActorSystem = System.create "MyActorSystem" (Configuration.load())

module Messages =
    type PlayMovieMessage = { Title: string; UserId: int }
    type StopMovieMessage() = class end

module Actors =
    type UserActor() as t =
        inherit ReceiveActor()
        let mutable watching = None
        do
            base.Receive<_>(Action<_>(t.HandlePlayMovieMessage))
            base.Receive<_>(Action<_>(t.HandleStopMovieMessage))
        member t.HandlePlayMovieMessage(msg: Messages.PlayMovieMessage) =
            match watching with
            | Some movie -> printfn "ERROR: already watching %s" movie
            | None -> watching <- Some msg.Title; printfn "Starting %s" msg.Title
        member t.HandleStopMovieMessage(msg: Messages.StopMovieMessage) =
            match watching with
            | Some title -> watching <- None; printfn "Stopping %s" title
            | None -> printfn "ERROR: nothing is playing"

[<EntryPoint>]
let main _ =
    let props = Props.Create<Actors.UserActor>()
    let actor = MyActorSystem.ActorOf(props, "PlaybackActor")
    actor.Tell({ Messages.PlayMovieMessage.Title = "Akka.NET: The Movie"; Messages.PlayMovieMessage.UserId = 1 })
    actor.Tell({ Messages.PlayMovieMessage.Title = "Another movie"; Messages.PlayMovieMessage.UserId = 1 })
    System.Console.ReadLine() |> ignore
    MyActorSystem.Terminate().Wait()
    0 // return an integer exit code
