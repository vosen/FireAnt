open FireAnt.Interfaces
open Orleans
open Orleans.Concurrency
open Orleans.Providers.Streams.SimpleMessageStream
open Orleans.Runtime.Configuration
open Orleans.Streams
open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open Xunit.Sdk

[<EntryPoint>]
let main argv =
    let config = ClientConfiguration.LocalhostSilo()
    config.AddSimpleMessageStreamProvider("SMSProvider", optimizeForImmutableData = true, pubSubType = StreamPubSubType.ExplicitGrainBasedOnly)
    GrainClient.Initialize(config);
    let streamProvider = GrainClient.GetStreamProvider("SMSProvider")
    let clientId = Guid.NewGuid()
    let stream = streamProvider.GetStream<string>(clientId, null)
    let finished = new ManualResetEventSlim()
    stream.SubscribeAsync((fun msg _ -> Console.WriteLine(msg); TaskDone.Done), (fun () -> finished.Set(); TaskDone.Done)) |> ignore
    let client = GrainClient.GrainFactory.GetGrain<IRemoteTestDispatcher>(clientId);
    client.Run("").Wait()
    //finished.Wait()
    0 // return an integer exit code
