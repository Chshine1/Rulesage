namespace Rulesage.Graph.Extensions

open System.Runtime.CompilerServices
open Microsoft.Extensions.DependencyInjection
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions
open Rulesage.Graph.Services.Implementations

type ServiceCollectionExtensions =
    [<Extension>]
    static member AddGraphModule(services: IServiceCollection) =
        services
            .AddScoped<ISimilarityService, SimilarityService>()
            .AddScoped<IGraphBuilder, GraphBuilder>()
            |> ignore
        services