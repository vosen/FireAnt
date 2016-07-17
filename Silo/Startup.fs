namespace FireAnt.Silo

open FireAnt.Grains
open FireAnt.Interfaces
open Microsoft.Extensions.DependencyInjection
open System
open System.IO

type NullTestTimeRepository() =
    interface ITestTimeRepository with
        member t.GetPredicted(name: string) : decimal =
            0m
        member t.Store(runId: string, test: string, time: decimal) : unit =
            ()

type NullWorkspaceBuilder() =
    interface IWorkspaceBuilder with
        member t.Build(runId: string) : FileInfo =
            FileInfo("NUL")

type Startup() = 
    member t.ConfigureServices(services: IServiceCollection) : IServiceProvider =
        services.AddSingleton<ITestTimeRepository>(NullTestTimeRepository())
                .AddSingleton<IWorkspaceBuilder>(NullWorkspaceBuilder())
                .AddTransient<Grains.RemoteTestDispatcher>() // This is fucking stupid and probably will get fixed
                .AddTransient<Grains.XunitRemoteTestRunner>() |> ignore
        services.BuildServiceProvider()